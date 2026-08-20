using System.Text;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Composition;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Projection.Markdown;

namespace Csharp2Md.Core.Analysis;

internal delegate FactValidationResult FragmentValidationFunc(FactValidationInput input);

public interface IAnalysisEngineObserver
{
    void ScopeStarted(string scope);
    void ScopeCompleted(string scope);
}

public sealed class AnalysisEngine
{
    private static readonly FactProvenance Provenance = new("csharp2md.syntax", "1");
    private readonly InertInventory _inventory;
    private readonly FragmentValidationFunc _validate;
    private readonly IAnalysisEngineObserver? _observer;
    private readonly IProjectEvaluationAdapter _evaluator;
    private readonly ISemanticCompilationAdapter _compilationAdapter;
    private readonly ISourceGeneratorAdapter _generatorAdapter;
    private readonly Action<SymbolIndex>? _onSymbolIndexBuilt;

    public AnalysisEngine()
        : this(new InertInventory(), FactValidator.Validate, null,
            new DotnetMsBuildEvaluator(), new SemanticCompilationAdapter(), new SourceGeneratorAdapter())
    {
    }

    public AnalysisEngine(IAnalysisEngineObserver? observer)
        : this(new InertInventory(), FactValidator.Validate, observer,
            new DotnetMsBuildEvaluator(), new SemanticCompilationAdapter(), new SourceGeneratorAdapter())
    {
    }

    internal AnalysisEngine(
        InertInventory inventory,
        FragmentValidationFunc validate,
        IAnalysisEngineObserver? observer,
        Action<SymbolIndex>? onSymbolIndexBuilt = null)
        : this(inventory, validate, observer,
            new DotnetMsBuildEvaluator(), new SemanticCompilationAdapter(), new SourceGeneratorAdapter(),
            onSymbolIndexBuilt)
    {
    }

    internal AnalysisEngine(
        InertInventory inventory,
        FragmentValidationFunc validate,
        IAnalysisEngineObserver? observer,
        IProjectEvaluationAdapter evaluator,
        ISemanticCompilationAdapter compilationAdapter,
        ISourceGeneratorAdapter generatorAdapter,
        Action<SymbolIndex>? onSymbolIndexBuilt = null)
    {
        _onSymbolIndexBuilt = onSymbolIndexBuilt;
        _inventory = inventory;
        _validate = validate;
        _observer = observer;
        _evaluator = evaluator;
        _compilationAdapter = compilationAdapter;
        _generatorAdapter = generatorAdapter;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var inventory = _inventory.Inventory(request);
        if (!inventory.IsSuccess)
        {
            return Result(request, AnalysisMode.SyntaxOnly, 1,
                "Inventory failed before output preparation.",
                inventory.Diagnostics.Select(static diagnostic => diagnostic.Message));
        }

        var inputRoot = Directory.Exists(request.Input)
            ? Path.GetFullPath(request.Input)
            : Path.GetDirectoryName(Path.GetFullPath(request.Input))!;
        try
        {
            new OutputWriter(request.OutputRoot).PrepareRun(inputRoot, request.ForceOutput);
        }
        catch (OutputPreparationException exception)
        {
            return Result(request, AnalysisMode.SyntaxOnly, 1, exception.Message, [exception.Message]);
        }

        var store = new FactStore(request.OutputRoot);
        var storedFragments = ImmutableArray.CreateBuilder<StoredFactFragment>();
        var coverageFacts = ImmutableArray.CreateBuilder<IFact>();
        var symbolFacts = ImmutableArray.CreateBuilder<SymbolFact>();
        var coverageOverrides = ImmutableArray.CreateBuilder<ScopeCoverageInput>();
        var analysisDiagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        var resultDiagnostics = new List<string>(inventory.Diagnostics.Select(static diagnostic => diagnostic.Message));
        var loadedExtensions = ImmutableArray.CreateBuilder<string>();
        var structuralFailure = false;
        var semanticSuccess = false;
        var documentCount = 0;
        var projectCount = 0;

        foreach (var service in inventory.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScopeStarted($"service:{service.RootPath}");
            try
            {
                foreach (var project in service.Projects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    projectCount++;
                    ScopeStarted($"project:{project.RelativePath}");
                    try
                    {
                        var projectResult = await AnalyzeProjectAsync(
                                request, project, store, storedFragments, coverageFacts, symbolFacts,
                                coverageOverrides, analysisDiagnostics, resultDiagnostics, loadedExtensions,
                                cancellationToken)
                            .ConfigureAwait(false);
                        documentCount += projectResult.DocumentCount;
                        structuralFailure |= projectResult.StructuralFailure;
                        semanticSuccess |= projectResult.SemanticSuccess;
                    }
                    finally
                    {
                        ScopeCompleted($"project:{project.RelativePath}");
                    }
                }
            }
            finally
            {
                ScopeCompleted($"service:{service.RootPath}");
            }
        }

        var effectiveMode = request.Options.Mode is AnalysisMode.Semantic && semanticSuccess
            ? AnalysisMode.Semantic
            : AnalysisMode.SyntaxOnly;
        resultDiagnostics.AddRange(analysisDiagnostics.Select(static diagnostic => diagnostic.Message));
        var accumulated = coverageFacts.ToImmutable();
        var honestCoverage = CoverageProjector.Project(new CoverageProjectionRequest(
            request.Options.Mode, accumulated, analysisDiagnostics.ToImmutable(), [], coverageOverrides.ToImmutable()));
        _onSymbolIndexBuilt?.Invoke(SymbolIndexBuilder.Build(
            symbolFacts,
            accumulated.OfType<ProjectFact>(),
            accumulated.OfType<DocumentFact>(),
            accumulated.OfType<TargetFact>()));
        var snapshot = new AggregateOutputSnapshot(
            request.Topic, request.Domain, "3.0.1", request.Options.Mode, effectiveMode, request.Options.Trust,
            loadedExtensions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            new ManifestCoverage(inventory.Services.Length, projectCount, documentCount),
            storedFragments.ToImmutable(), honestCoverage);
        new CanonicalAggregateWriter().WritePrepared(request.OutputRoot, snapshot, TimeProvider.System);

        return Result(
            request,
            effectiveMode,
            structuralFailure ? 1 : 0,
            $"Analyzed {projectCount} project(s) and {documentCount} document(s); wrote {storedFragments.Count} factual fragment(s).",
            resultDiagnostics);
    }

    private async Task<ProjectAnalysisResult> AnalyzeProjectAsync(
        AnalysisRequest request,
        InventoryProject project,
        FactStore store,
        ImmutableArray<StoredFactFragment>.Builder storedFragments,
        ImmutableArray<IFact>.Builder coverageFacts,
        ImmutableArray<SymbolFact>.Builder symbolFacts,
        ImmutableArray<ScopeCoverageInput>.Builder coverageOverrides,
        ImmutableArray<AnalysisDiagnostic>.Builder analysisDiagnostics,
        List<string> resultDiagnostics,
        ImmutableArray<string>.Builder loadedExtensions,
        CancellationToken cancellationToken)
    {
        var projectId = ProjectFactId.Create(project.RelativePath);
        var sources = ImmutableArray.CreateBuilder<SemanticProjectDocument>();
        foreach (var relativeSourcePath in project.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = File.ReadAllText(project.SourcePaths[relativeSourcePath]);
            sources.Add(new SemanticProjectDocument(
                relativeSourcePath,
                source,
                SyntaxFactExtractor.Extract(projectId, relativeSourcePath, source)));
        }

        var syntacticProject = new ProjectFact(
            FactHeader.Create(projectId.ToFactId(), FactKind.Project, FactResolution.Syntactic, [Provenance]),
            projectId,
            project.Name,
            project.RelativePath,
            [],
            sources.Select(static source => source.Extraction.Document.DocumentId).ToImmutableArray());
        var processed = request.Options.Mode is AnalysisMode.Semantic
            ? await new TrustedSemanticProjectProcessor(
                    _evaluator, _compilationAdapter, _generatorAdapter, _observer)
                .ProcessAsync(project, syntacticProject, sources.ToImmutable(), request.Options, cancellationToken)
                .ConfigureAwait(false)
            : SyntaxOnly(syntacticProject, sources.ToImmutable());

        analysisDiagnostics.AddRange(processed.Diagnostics);
        loadedExtensions.AddRange(processed.LoadedExtensions);
        var structuralFailure = false;
        var persistedDocumentIds = ImmutableArray.CreateBuilder<DocumentFactId>();
        foreach (var document in processed.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = document.Source.RelativePath;
            ScopeStarted($"document:{relativePath}");
            try
            {
                var extraction = document.Source.Extraction;
                IFact[] syntacticFacts =
                [
                    extraction.Document,
                    .. extraction.Document.Sections,
                    .. extraction.Symbols,
                ];
                if (syntacticFacts.GroupBy(static fact => fact.Header.Id).Any(static group => group.Skip(1).Any()))
                {
                    var syntaxValidation = _validate(FactValidationInput.Create(
                        syntacticFacts,
                        documents: [DocumentExtent.Create(
                            extraction.Document.DocumentId,
                            relativePath,
                            LineLengths(document.Source.SourceText))],
                        knownFactIds: [projectId.ToFactId()]));
                    analysisDiagnostics.AddRange(syntaxValidation.ValidationDiagnostics);
                    if (!syntaxValidation.IsValid)
                    {
                        structuralFailure = true;
                        resultDiagnostics.AddRange(syntaxValidation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
                        continue;
                    }
                }

                var linkedIds = document.LinkedSymbolIds.IsEmpty ? extraction.Document.SymbolIds : document.LinkedSymbolIds;
                var documentFact = extraction.Document with { SymbolIds = linkedIds };
                var errorIds = document.EnrichedSymbols
                    .Where(static symbol => symbol.ContainsErrorSymbol)
                    .Select(static symbol => symbol.SymbolId)
                    .ToHashSet();
                var relationFacts = RelationCollector.CreateFacts(
                    extraction.Document.DocumentId, relativePath, extraction.RelationCandidates);
                var baseline = new IFact[] { documentFact }
                    .Concat(extraction.Document.Sections)
                    .Concat(extraction.Symbols.Where(symbol => !errorIds.Contains(symbol.SymbolId)))
                    .Concat(relationFacts);
                var enrichment = document.EnrichedSymbols.Cast<IFact>().Concat(document.EnrichedRelations);
                var merged = FactMerger.Merge(baseline, enrichment, document.Diagnostics);
                analysisDiagnostics.AddRange(merged.StructuralDiagnostics);
                if (!merged.IsValid)
                {
                    structuralFailure = true;
                    resultDiagnostics.AddRange(merged.StructuralDiagnostics.Select(static diagnostic => diagnostic.Message));
                    continue;
                }

                var validation = _validate(FactValidationInput.Create(
                    merged.Facts,
                    diagnostics: merged.Diagnostics,
                    documents: [DocumentExtent.Create(documentFact.DocumentId, relativePath, LineLengths(document.Source.SourceText))],
                    knownFactIds: [projectId.ToFactId()]));
                analysisDiagnostics.AddRange(validation.ValidationDiagnostics);
                if (!validation.IsValid)
                {
                    structuralFailure = true;
                    resultDiagnostics.AddRange(validation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
                    continue;
                }

                var fragment = validation.Fragment!;
                var stored = store.Persist(fragment);
                storedFragments.Add(stored);
                persistedDocumentIds.Add(documentFact.DocumentId);
                coverageFacts.Add(fragment.Facts.OfType<DocumentFact>().Single());
                symbolFacts.AddRange(fragment.Facts.OfType<SymbolFact>());
                coverageOverrides.Add(new ScopeCoverageInput(
                    documentFact.DocumentId.ToFactId(), CoverageApplicability.Applicable,
                    document.Attempted ? CoverageAttempt.Attempted : CoverageAttempt.NotAttempted));
                WriteMarkdown(request.OutputRoot, relativePath, FrontmatterV2.Create(fragment, stored), MarkdownProjector.Project(fragment));
            }
            finally
            {
                ScopeCompleted($"document:{relativePath}");
            }
        }

        var projectFact = processed.Project.Project with
        {
            DocumentIds = persistedDocumentIds.Distinct().OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray(),
        };
        var projectMerge = FactMerger.Merge([projectFact, .. processed.Project.Targets], diagnostics: processed.Project.Diagnostics);
        analysisDiagnostics.AddRange(projectMerge.StructuralDiagnostics);
        if (!projectMerge.IsValid)
        {
            structuralFailure = true;
            resultDiagnostics.AddRange(projectMerge.StructuralDiagnostics.Select(static diagnostic => diagnostic.Message));
        }
        else
        {
            var validation = _validate(FactValidationInput.Create(
                projectMerge.Facts,
                diagnostics: projectMerge.Diagnostics,
                knownFactIds: persistedDocumentIds.Select(static id => id.ToFactId())));
            analysisDiagnostics.AddRange(validation.ValidationDiagnostics);
            if (validation.IsValid)
            {
                var fragment = validation.Fragment!;
                storedFragments.Add(store.Persist(fragment));
                coverageFacts.AddRange(fragment.Facts.Where(static fact => fact is ProjectFact or TargetFact));
            }
            else
            {
                structuralFailure = true;
                resultDiagnostics.AddRange(validation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
            }
        }

        return new ProjectAnalysisResult(processed.Documents.Length, structuralFailure, processed.AnySemanticSuccess);
    }

    private static TrustedSemanticProjectResult SyntaxOnly(
        ProjectFact project,
        ImmutableArray<SemanticProjectDocument> sources) =>
        new(
            new ProjectFactEnrichmentResult(project, [], []),
            sources.Select(static source => new SemanticProcessedDocument(source, false, [], [], [], [])).ToImmutableArray(),
            [], [], false);

    private static AnalysisResult Result(
        AnalysisRequest request,
        AnalysisMode effectiveMode,
        int exitCode,
        string summary,
        IEnumerable<string> diagnostics) =>
        new(exitCode, request.Options.Mode, effectiveMode, summary,
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray());

    private static IEnumerable<int> LineLengths(string source)
    {
        using var reader = new StringReader(source);
        string? line;
        var any = false;
        while ((line = reader.ReadLine()) is not null)
        {
            any = true;
            yield return line.Length;
        }

        if (!any || source.EndsWith('\n'))
        {
            yield return 0;
        }
    }

    private static void WriteMarkdown(
        string outputRoot,
        string relativeSourcePath,
        FrontmatterV2 frontmatter,
        string markdown)
    {
        var path = Path.Combine(
            outputRoot,
            "raw",
            "codebase",
            relativeSourcePath.Replace('/', Path.DirectorySeparatorChar)) + ".md";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, frontmatter.ToYaml() + '\n' + markdown, new UTF8Encoding(false));
    }

    private void ScopeStarted(string scope) => _observer?.ScopeStarted(scope);
    private void ScopeCompleted(string scope) => _observer?.ScopeCompleted(scope);

    private sealed record ProjectAnalysisResult(int DocumentCount, bool StructuralFailure, bool SemanticSuccess);
}
