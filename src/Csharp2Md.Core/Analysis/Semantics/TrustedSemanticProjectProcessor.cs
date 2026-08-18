using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Semantics;

internal sealed record SemanticProjectDocument(
    string RelativePath,
    string SourceText,
    SyntaxFactExtraction Extraction,
    bool Generated = false);

internal sealed record SemanticProcessedDocument(
    SemanticProjectDocument Source,
    bool Attempted,
    ImmutableArray<SymbolFact> EnrichedSymbols,
    ImmutableArray<SymbolFactId> LinkedSymbolIds,
    ImmutableArray<AnalysisDiagnostic> Diagnostics);

internal sealed record TrustedSemanticProjectResult(
    ProjectFactEnrichmentResult Project,
    ImmutableArray<SemanticProcessedDocument> Documents,
    ImmutableArray<AnalysisDiagnostic> Diagnostics,
    ImmutableArray<string> LoadedExtensions,
    bool AnySemanticSuccess);

internal sealed class TrustedSemanticProjectProcessor(
    IProjectEvaluationAdapter evaluator,
    ISemanticCompilationAdapter compilationAdapter,
    ISourceGeneratorAdapter generatorAdapter,
    IAnalysisEngineObserver? observer = null)
{
    public async Task<TrustedSemanticProjectResult> ProcessAsync(
        InventoryProject inventory,
        ProjectFact syntacticProject,
        ImmutableArray<SemanticProjectDocument> sourceDocuments,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(syntacticProject);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var evaluation = await EvaluateAsync(inventory, syntacticProject.ProjectId, options, cancellationToken)
            .ConfigureAwait(false);
        if (evaluation.Targets.IsEmpty && evaluation.Diagnostics.IsEmpty)
        {
            evaluation = evaluation with
            {
                Diagnostics =
                [
                    Diagnostic(
                        "C2M-ENGINE-006",
                        DiagnosticStage.Evaluation,
                        syntacticProject.ProjectId.ToFactId(),
                        "Trusted semantic analysis produced no evaluable targets; the syntax-only migration cut was retained.",
                        string.Empty),
                ],
            };
        }

        var project = ProjectFactEnricher.Enrich(syntacticProject, evaluation);
        var diagnostics = project.Diagnostics.ToBuilder();
        var loadedExtensions = ImmutableArray.CreateBuilder<string>();
        var states = sourceDocuments.ToDictionary(
            static document => document.Extraction.Document.DocumentId,
            static document => new DocumentState(document));

        foreach (var target in evaluation.Targets.OrderBy(static target => target.TargetId.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!target.Succeeded)
            {
                continue;
            }

            var scope = $"semantic:{target.TargetId.Value}";
            observer?.ScopeStarted(scope);
            try
            {
                var selected = SelectCompileDocuments(inventory, target, states.Values);
                foreach (var state in selected)
                {
                    state.Attempted = true;
                }

                var compilation = CreateCompilation(target, selected, diagnostics, cancellationToken);
                if (compilation is null)
                {
                    AddUnavailableDocumentDiagnostics(selected, target.TargetId, diagnostics);
                    continue;
                }

                diagnostics.AddRange(compilation.Diagnostics);
                var bindingCompilation = RunGenerators(
                    syntacticProject.ProjectId,
                    target,
                    evaluation.GeneratorPaths,
                    compilation,
                    states,
                    diagnostics,
                    loadedExtensions,
                    options,
                    cancellationToken);
                BindDocuments(target.TargetId, bindingCompilation, states, diagnostics, cancellationToken);
            }
            finally
            {
                observer?.ScopeCompleted(scope);
            }
        }

        return new TrustedSemanticProjectResult(
            project,
            states.Values
                .OrderBy(static state => state.Source.RelativePath, StringComparer.Ordinal)
                .Select(static state => state.ToResult())
                .ToImmutableArray(),
            diagnostics
                .GroupBy(static diagnostic => diagnostic.Id)
                .Select(static group => group.First())
                .Order()
                .ToImmutableArray(),
            loadedExtensions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            evaluation.Targets.Any(static target => target.Succeeded));
    }

    private async Task<ProjectEvaluationResult> EvaluateAsync(
        InventoryProject inventory,
        ProjectFactId projectId,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = TrustedProjectEvaluationRequest.Create(projectId, inventory.ProjectPath, options);
            return await evaluator.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var diagnostic = Diagnostic(
                "C2M-ENGINE-001",
                DiagnosticStage.Evaluation,
                projectId.ToFactId(),
                "Trusted project evaluation failed; syntax facts were retained.",
                exception.GetType().Name);
            return new ProjectEvaluationResult(
                projectId,
                string.Empty,
                [],
                [],
                [],
                [],
                [],
                [diagnostic],
                TimedOut: false);
        }
    }

    private SemanticCompilationResult? CreateCompilation(
        EvaluatedTarget target,
        ImmutableArray<DocumentState> documents,
        ImmutableArray<AnalysisDiagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            return compilationAdapter.CreateCompilation(
                new SemanticCompilationRequest(
                    target,
                    target.Properties.GetValueOrDefault("AssemblyName", string.Empty) is { Length: > 0 } assemblyName
                        ? assemblyName
                        : "csharp2md-analysis",
                    documents.Select(static state => new SemanticSourceDocument(
                        state.Source.Extraction.Document.DocumentId,
                        state.Source.RelativePath,
                        state.Source.SourceText)).ToImmutableArray()),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(Diagnostic(
                "C2M-ENGINE-002",
                DiagnosticStage.Compilation,
                target.TargetId.ToFactId(),
                "Semantic compilation failed; syntax facts were retained.",
                exception.GetType().Name));
            return null;
        }
    }

    private SemanticCompilationResult RunGenerators(
        ProjectFactId projectId,
        EvaluatedTarget target,
        ImmutableArray<string> declaredGeneratorPaths,
        SemanticCompilationResult compilation,
        Dictionary<DocumentFactId, DocumentState> states,
        ImmutableArray<AnalysisDiagnostic>.Builder diagnostics,
        ImmutableArray<string>.Builder loadedExtensions,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var generatorRequest = SourceGeneratorExecutionRequest.Create(
            projectId,
            compilation,
            declaredGeneratorPaths
                .Concat(target.Items.GetValueOrDefault("Generator", [])
                    .Select(static item => item.Identity))
                .Concat(target.Items.GetValueOrDefault("Analyzer", [])
                    .Where(static item => string.Equals(item.Metadata.GetValueOrDefault("ExtensionKind"), "Generator", StringComparison.OrdinalIgnoreCase))
                    .Select(static item => item.Identity)),
            options);
        if (generatorRequest is null)
        {
            return compilation;
        }

        SourceGeneratorExecutionResult generated;
        try
        {
            generated = generatorAdapter.Run(generatorRequest, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(Diagnostic(
                "C2M-ENGINE-003",
                DiagnosticStage.Generator,
                target.TargetId.ToFactId(),
                "Source-generator execution failed; pre-generator facts were retained.",
                exception.GetType().Name));
            return compilation;
        }

        diagnostics.AddRange(generated.Diagnostics);
        loadedExtensions.AddRange(generated.LoadedExtensions);
        if (generated.GeneratedDocuments.IsEmpty)
        {
            return compilation;
        }

        foreach (var generatedDocument in generated.GeneratedDocuments)
        {
            if (states.ContainsKey(generatedDocument.DocumentId))
            {
                continue;
            }

            var extraction = SyntaxFactExtractor.Extract(
                projectId,
                generatedDocument.RelativePath,
                generatedDocument.SourceText);
            states.Add(
                generatedDocument.DocumentId,
                new DocumentState(new SemanticProjectDocument(
                    generatedDocument.RelativePath,
                    generatedDocument.SourceText,
                    extraction,
                    Generated: true))
                {
                    Attempted = true,
                });
        }

        var selected = states.Values.Where(static state => state.Attempted).ToImmutableArray();
        return CreateCompilation(target, selected, diagnostics, cancellationToken) ?? compilation;
    }

    private static void BindDocuments(
        TargetFactId targetId,
        SemanticCompilationResult compilation,
        Dictionary<DocumentFactId, DocumentState> states,
        ImmutableArray<AnalysisDiagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var bindings = compilation.Documents.ToDictionary(static binding => binding.DocumentId);
        foreach (var state in states.Values.Where(static state => state.Attempted))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!bindings.TryGetValue(state.Source.Extraction.Document.DocumentId, out var binding))
            {
                var missing = Diagnostic(
                    "C2M-ENGINE-004",
                    DiagnosticStage.Document,
                    state.Source.Extraction.Document.DocumentId.ToFactId(),
                    "No semantic binding was produced for the document; syntax facts were retained.",
                    targetId.Value);
                state.Diagnostics.Add(missing);
                diagnostics.Add(missing);
                continue;
            }

            try
            {
                var enriched = new SymbolFactEnricher().Enrich(
                    state.Source.Extraction.Document,
                    state.Source.Extraction.Symbols,
                    binding,
                    targetId,
                    cancellationToken);
                state.EnrichedSymbols.AddRange(enriched.Symbols);
                state.LinkedSymbolIds.AddRange(enriched.Document.SymbolIds);
                state.Diagnostics.AddRange(enriched.Diagnostics);
                diagnostics.AddRange(enriched.Diagnostics);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var failed = Diagnostic(
                    "C2M-ENGINE-005",
                    DiagnosticStage.Document,
                    state.Source.Extraction.Document.DocumentId.ToFactId(),
                    "Semantic fact enrichment failed for the document; syntax facts were retained.",
                    exception.GetType().Name);
                state.Diagnostics.Add(failed);
                diagnostics.Add(failed);
            }
        }
    }

    private static ImmutableArray<DocumentState> SelectCompileDocuments(
        InventoryProject project,
        EvaluatedTarget target,
        IEnumerable<DocumentState> states)
    {
        if (!target.Items.TryGetValue("Compile", out var compileItems))
        {
            return states.ToImmutableArray();
        }

        var compilePaths = compileItems
            .SelectMany(item => CandidatePaths(project.ProjectPath, item))
            .Select(Path.GetFullPath)
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        return states
            .Where(state => project.SourcePaths.TryGetValue(state.Source.RelativePath, out var path)
                && compilePaths.Contains(Path.GetFullPath(path)))
            .ToImmutableArray();
    }

    private static IEnumerable<string> CandidatePaths(string projectPath, EvaluatedItem item)
    {
        if (item.Metadata.TryGetValue("FullPath", out var fullPath) && Path.IsPathFullyQualified(fullPath))
        {
            yield return fullPath;
        }

        yield return Path.IsPathFullyQualified(item.Identity)
            ? item.Identity
            : Path.GetFullPath(item.Identity, Path.GetDirectoryName(projectPath)!);
    }

    private static void AddUnavailableDocumentDiagnostics(
        ImmutableArray<DocumentState> states,
        TargetFactId targetId,
        ImmutableArray<AnalysisDiagnostic>.Builder diagnostics)
    {
        foreach (var state in states)
        {
            var diagnostic = Diagnostic(
                "C2M-ENGINE-004",
                DiagnosticStage.Document,
                state.Source.Extraction.Document.DocumentId.ToFactId(),
                "No semantic binding was produced for the document; syntax facts were retained.",
                targetId.Value);
            state.Diagnostics.Add(diagnostic);
            diagnostics.Add(diagnostic);
        }
    }

    private static AnalysisDiagnostic Diagnostic(
        string code,
        DiagnosticStage stage,
        FactId scopeId,
        string message,
        string detail) =>
        AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Warning,
            stage,
            scopeId,
            message,
            string.IsNullOrWhiteSpace(detail) ? [] : [new DiagnosticData("detail", detail)]);

    private sealed class DocumentState(SemanticProjectDocument source)
    {
        public SemanticProjectDocument Source { get; } = source;
        public bool Attempted { get; set; }
        public ImmutableArray<SymbolFact>.Builder EnrichedSymbols { get; } = ImmutableArray.CreateBuilder<SymbolFact>();
        public ImmutableArray<SymbolFactId>.Builder LinkedSymbolIds { get; } = ImmutableArray.CreateBuilder<SymbolFactId>();
        public ImmutableArray<AnalysisDiagnostic>.Builder Diagnostics { get; } = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();

        public SemanticProcessedDocument ToResult() => new(
            Source,
            Attempted,
            EnrichedSymbols.ToImmutable(),
            LinkedSymbolIds.Distinct().OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray(),
            Diagnostics.GroupBy(static diagnostic => diagnostic.Id).Select(static group => group.First()).Order().ToImmutableArray());
    }
}
