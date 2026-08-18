using System.Text;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Inventory;
using Csharp2Md.Core.Analysis.Syntax;
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

internal interface IAnalysisEngineObserver
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

    public AnalysisEngine()
        : this(new InertInventory(), FactValidator.Validate, null)
    {
    }

    internal AnalysisEngine(
        InertInventory inventory,
        FragmentValidationFunc validate,
        IAnalysisEngineObserver? observer)
    {
        _inventory = inventory;
        _validate = validate;
        _observer = observer;
    }

    public Task<AnalysisResult> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var inventory = _inventory.Inventory(request);
        if (!inventory.IsSuccess)
        {
            return Task.FromResult(Result(
                request,
                AnalysisMode.SyntaxOnly,
                1,
                "Inventory failed before output preparation.",
                inventory.Diagnostics.Select(static diagnostic => diagnostic.Message)));
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
            return Task.FromResult(Result(request, AnalysisMode.SyntaxOnly, 1, exception.Message, [exception.Message]));
        }

        var store = new FactStore(request.OutputRoot);
        var storedFragments = ImmutableArray.CreateBuilder<StoredFactFragment>();
        var resultDiagnostics = new List<string>(inventory.Diagnostics.Select(static diagnostic => diagnostic.Message));
        var structuralFailure = false;
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
                    var projectId = ProjectFactId.Create(project.RelativePath);
                    var documentIds = ImmutableArray.CreateBuilder<DocumentFactId>();
                    ScopeStarted($"project:{project.RelativePath}");
                    try
                    {
                        foreach (var relativeSourcePath in project.SourceFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            documentCount++;
                            ScopeStarted($"document:{relativeSourcePath}");
                            try
                            {
                                var source = File.ReadAllText(project.SourcePaths[relativeSourcePath]);
                                var extraction = SyntaxFactExtractor.Extract(projectId, relativeSourcePath, source);
                                documentIds.Add(extraction.Document.DocumentId);
                                IFact[] facts =
                                [
                                    extraction.Document,
                                    .. extraction.Document.Sections,
                                    .. extraction.Symbols,
                                ];
                                var validation = _validate(FactValidationInput.Create(
                                    facts,
                                    documents: [DocumentExtent.Create(extraction.Document.DocumentId, relativeSourcePath, LineLengths(source))],
                                    knownFactIds: [projectId.ToFactId()]));
                                if (!validation.IsValid)
                                {
                                    structuralFailure = true;
                                    resultDiagnostics.AddRange(validation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
                                    continue;
                                }

                                var fragment = validation.Fragment!;
                                var stored = store.Persist(fragment);
                                storedFragments.Add(stored);
                                WriteMarkdown(request.OutputRoot, relativeSourcePath, FrontmatterV2.Create(fragment, stored), MarkdownProjector.Project(fragment));
                            }
                            finally
                            {
                                ScopeCompleted($"document:{relativeSourcePath}");
                            }
                        }

                        var projectFact = new ProjectFact(
                            FactHeader.Create(projectId.ToFactId(), FactKind.Project, FactResolution.Syntactic, [Provenance]),
                            projectId,
                            project.Name,
                            project.RelativePath,
                            [],
                            documentIds.Distinct().OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray());
                        var projectValidation = _validate(FactValidationInput.Create(
                            [projectFact], knownFactIds: documentIds.Select(static id => id.ToFactId())));
                        if (projectValidation.IsValid)
                        {
                            storedFragments.Add(store.Persist(projectValidation.Fragment!));
                        }
                        else
                        {
                            structuralFailure = true;
                            resultDiagnostics.AddRange(projectValidation.ValidationDiagnostics.Select(static diagnostic => diagnostic.Message));
                        }
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

        var effectiveMode = AnalysisMode.SyntaxOnly;
        if (request.Options.Mode == AnalysisMode.Semantic)
        {
            resultDiagnostics.Add("Trusted semantic analysis is not available in this syntax-only migration cut; syntax facts were retained.");
        }

        var snapshot = new AggregateOutputSnapshot(
            request.Topic,
            request.Domain,
            "3.0.0",
            request.Options.Mode,
            effectiveMode,
            request.Options.Trust,
            [],
            new ManifestCoverage(inventory.Services.Length, projectCount, documentCount),
            storedFragments.ToImmutable());
        new CanonicalAggregateWriter().WritePrepared(request.OutputRoot, snapshot, TimeProvider.System);

        var exitCode = structuralFailure ? 1 : 0;
        return Task.FromResult(Result(
            request,
            effectiveMode,
            exitCode,
            $"Analyzed {projectCount} project(s) and {documentCount} document(s); wrote {storedFragments.Count} factual fragment(s).",
            resultDiagnostics));
    }

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
}
