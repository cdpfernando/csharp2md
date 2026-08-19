using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Discovery;
using Csharp2Md.Core.Graph;
using Csharp2Md.Core.Loading;
using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Rendering;
using Csharp2Md.Core.Topic;
using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Pipeline;

/// <summary>
/// Loads one service's solution. A delegate rather than a call to <see cref="SolutionLoader"/>
/// so the sequential-workspace invariant (P1-19) can be observed from a test: the loader owns its
/// <c>MSBuildWorkspace</c> for exactly the duration of the call, so non-overlapping calls mean
/// non-overlapping workspaces.
/// </summary>
public delegate Task<LoadedService> SolutionLoadFunc(string solutionPath, CancellationToken cancellationToken);

/// <summary>
/// Outcome of one pipeline run. A failed manifest is an expected error, never an exception
/// (P1-16), and nothing is written when it happens.
/// </summary>
public sealed record PipelineRunResult(
    ManifestError? ManifestError,
    LoadReport LoadReport,
    DependencyGraph Graph,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<FrontmatterFailure> FrontmatterFailures,
    int DocumentCount,
    int ServiceCount)
{
    public bool IsSuccess => ManifestError is null;

    /// <summary>
    /// WIKI-12: the exit code this run warrants — <c>1</c> when the manifest itself failed (nothing
    /// written) or one or more documents failed frontmatter validation (everything else still
    /// written); <c>0</c> otherwise. A degraded project load (AD-005) is neither condition, so it
    /// stays <c>0</c> with warnings — the two failure modes are never conflated.
    /// </summary>
    public int ExitCode => !IsSuccess || FrontmatterFailures.Count > 0 ? 1 : 0;

    public static PipelineRunResult Failed(ManifestError error) =>
        new(error, new LoadReport([]), new DependencyGraph([]), [], [], 0, 0);
}

/// <summary>
/// Runs the three stages end to end: Inventory, Analysis, Aggregate (AD-001).
/// </summary>
/// <remarks>
/// <para>
/// Stage 1 completes in full before any detector runs, because internal-package matching (P2-04)
/// and logical-name resolution (P2-06) both need every service known first.
/// </para>
/// <para>
/// Stage 2 is sequential and streaming: one service's workspace at a time (P1-19), and each
/// rendered document is written to disk and dropped rather than accumulated, so memory is bounded
/// by the catalog, the accumulated signals and the index entries — not by codebase size (AD-001).
/// </para>
/// </remarks>
public sealed class AnalysisPipeline
{
    private readonly SolutionLoadFunc _loadAsync;
    private readonly MarkdownRenderer _renderer = new();

    private readonly IReadOnlyList<IDocumentDependencyDetector> _documentDetectors =
        [new HttpClientDetector(), new GrpcClientDetector(), new MessagingDetector()];

    private readonly IProjectDependencyDetector _projectDetector = new DirectReferenceDetector();

    public AnalysisPipeline()
        : this(new SolutionLoader().LoadAsync)
    {
    }

    public AnalysisPipeline(SolutionLoadFunc loadAsync)
    {
        ArgumentNullException.ThrowIfNull(loadAsync);
        _loadAsync = loadAsync;
    }

    public async Task<PipelineRunResult> RunAsync(
        string manifestPath,
        string outputRoot,
        CancellationToken cancellationToken = default,
        bool forceOutput = false,
        TopicOptions? topicOptions = null)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);

        // ── Stage 1: Inventory ──────────────────────────────────────────────────────────────
        var manifest = ManifestLoader.Load(manifestPath);
        if (!manifest.IsSuccess)
        {
            // P1-16: fail before anything is written, so an invalid manifest leaves no output.
            return PipelineRunResult.Failed(manifest.Error!.Value);
        }

        var inputRoot = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        return await RunAsync(
            manifest.Manifest!, inputRoot, outputRoot, cancellationToken, forceOutput, topicOptions);
    }

    public async Task<PipelineRunResult> RunAsync(
        Manifest manifest,
        string inputRoot,
        string outputRoot,
        CancellationToken cancellationToken = default,
        bool forceOutput = false,
        TopicOptions? topicOptions = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(inputRoot);
        ArgumentNullException.ThrowIfNull(outputRoot);

        // WIKI-06/WIKI-14..16: callers that already validated --topic/--domain pass the result
        // through; every other caller (every pre-existing test, T13's own integration tests) gets
        // the zero-config default so frontmatter derivation never needs a caller-supplied value.
        var options = topicOptions ?? TopicOptions.Default(inputRoot);

        var discovery = ServiceDiscoverer.Discover(manifest);
        var warnings = new List<string>(discovery.Warnings);
        var catalog = Inventory(discovery.Catalog, warnings);
        var config = ConfigIndexer.Index(catalog);
        warnings.AddRange(config.Warnings);

        // ── Stage 2: Analysis ───────────────────────────────────────────────────────────────
        new OutputWriter(outputRoot).PrepareRun(inputRoot, forceOutput);

        var signals = new List<DependencySignal>();
        var loadResults = new List<ProjectLoadResult>();
        var serviceIndexes = new List<ServiceIndexEntry>();
        var frontmatterFailures = new List<FrontmatterFailure>();
        var documentCount = 0;

        foreach (var service in catalog.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (indexPath, serviceDocumentCount) = await AnalyzeAsync(
                service, catalog, config.Index, outputRoot, options, signals, loadResults, warnings,
                frontmatterFailures, cancellationToken);

            serviceIndexes.Add(new ServiceIndexEntry(service.Name, indexPath));
            documentCount += serviceDocumentCount;
        }

        // ── Stage 3: Aggregate ──────────────────────────────────────────────────────────────
        // WIKI-01..03: dependencies.json/.mmd sit at the root of raw/, while the root index.md
        // stays part of the mirrored raw/codebase/ tree it indexes, alongside every service's own.
        var graph = GraphBuilder.Build(signals);
        var rawRoot = TopicLayout.RawRoot(outputRoot);
        DependencyJsonWriter.Write(graph, rawRoot);
        MermaidWriter.Write(graph, rawRoot);
        IndexWriter.WriteRootIndex(TopicLayout.CodebaseRoot(outputRoot), serviceIndexes, options);

        return new PipelineRunResult(
            null, new LoadReport(loadResults), graph, warnings, frontmatterFailures,
            documentCount, catalog.Services.Count);
    }

    /// <summary>
    /// Completes the catalog: drops project paths that do not exist on disk (P1-06's rule, applied
    /// before Roslyn is involved at all) and reads each remaining project's <c>PackageId</c>, which
    /// P2-04's internal-package matching needs before any detector runs.
    /// </summary>
    private static ServiceCatalog Inventory(ServiceCatalog catalog, List<string> warnings)
    {
        var services = new List<ServiceDescriptor>(catalog.Services.Count);

        foreach (var service in catalog.Services)
        {
            var projectPaths = new List<string>(service.ProjectPaths.Count);
            foreach (var path in service.ProjectPaths)
            {
                if (File.Exists(path))
                {
                    projectPaths.Add(path);
                }
                else
                {
                    warnings.Add($"Project file not found, skipped: {path}");
                }
            }

            var complete = service with { ProjectPaths = projectPaths };
            services.Add(complete with { PackageIds = ProjectIdentityReader.ReadPackageIds(complete) });
        }

        return new ServiceCatalog(services);
    }

    /// <summary>Analyzes one service and returns the path of its written <c>index.md</c> and the count of source documents written.</summary>
    private async Task<(string IndexPath, int DocumentCount)> AnalyzeAsync(
        ServiceDescriptor service,
        ServiceCatalog catalog,
        ConfigIndex configIndex,
        string outputRoot,
        TopicOptions options,
        List<DependencySignal> signals,
        List<ProjectLoadResult> loadResults,
        List<string> warnings,
        List<FrontmatterFailure> frontmatterFailures,
        CancellationToken cancellationToken)
    {
        var serviceOutputRoot = TopicLayout.ServiceRoot(outputRoot, service.Name);
        var writer = new OutputWriter(serviceOutputRoot);
        var writtenPaths = new List<string>();

        // Project-level detection reads project files as XML — no Roslyn, no workspace.
        foreach (var projectPath in service.ProjectPaths)
        {
            signals.AddRange(_projectDetector.Detect(
                new ProjectDetectionContext(service.Name, projectPath, catalog)));
        }

        using var solution = SynthesizedSolution.For(service);
        var loaded = await _loadAsync(solution.Path, cancellationToken);
        loadResults.AddRange(loaded.Report.Projects);

        var owned = new HashSet<string>(
            service.ProjectPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);

        foreach (var project in loaded.Solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A project pulled in transitively by a reference belongs to whichever service owns it,
            // not to this one — rendering it here would duplicate its documents across services.
            if (project.FilePath is null || !owned.Contains(Path.GetFullPath(project.FilePath)))
            {
                continue;
            }

            // P1-09: a project Roslyn cannot compile at all loses its documents, nothing else.
            if (!project.SupportsCompilation)
            {
                continue;
            }

            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await AnalyzeDocumentAsync(
                    document, service, project, configIndex, writer, options, writtenPaths, signals, warnings,
                    frontmatterFailures, cancellationToken);
            }
        }

        var indexPath = IndexWriter.WriteServiceIndex(serviceOutputRoot, service.Name, writtenPaths, options); // P1-13
        return (indexPath, writtenPaths.Count);
    }

    private async Task AnalyzeDocumentAsync(
        Document document,
        ServiceDescriptor service,
        Project project,
        ConfigIndex configIndex,
        OutputWriter writer,
        TopicOptions options,
        List<string> writtenPaths,
        List<DependencySignal> signals,
        List<string> warnings,
        List<FrontmatterFailure> frontmatterFailures,
        CancellationToken cancellationToken)
    {
        if (document.FilePath is null || !document.SupportsSyntaxTree)
        {
            return;
        }

        var relativePath = RelativePathFor(service, project, document.FilePath);
        if (OutputWriter.IsExcluded(relativePath))
        {
            return;
        }

        if (await document.GetSyntaxTreeAsync(cancellationToken) is not { } syntaxTree)
        {
            return;
        }

        var semanticModel = document.SupportsSemanticModel
            ? await document.GetSemanticModelAsync(cancellationToken)
            : null;

        var detectionContext = new DocumentDetectionContext(
            service.Name, document.FilePath, syntaxTree, semanticModel, configIndex);

        var documentSignals = _documentDetectors
            .SelectMany(detector => detector.Detect(detectionContext))
            .ToList();

        signals.AddRange(documentSignals);

        var renderContext = new RenderContext(relativePath, syntaxTree, semanticModel);
        var rendered = DependencySectionRenderer.Apply(
            SemanticEnricher.Enrich(_renderer.Render(renderContext), renderContext), documentSignals);

        // WIKI-06/WIKI-09/WIKI-13: derived from the tree already materialized above for the
        // detectors — no second parse, no re-read of the file writer.Write is about to produce.
        // project.Name is the SDK-style project's default root namespace absent an explicit
        // <RootNamespace> override, matching every fixture project (AnalysisPipeline.cs already
        // relies on the same property at RelativePathFor).
        var sourcePath = $"{service.Name.Value}/{relativePath}";
        var frontmatter = FrontmatterBuilder.Build(
            syntaxTree, sourcePath, project.Name, options, out var frontmatterWarnings);
        warnings.AddRange(frontmatterWarnings);
        rendered = rendered with { Frontmatter = frontmatter };

        // WIKI-12: a validation failure never aborts the run — it is collected here and the loop
        // continues to the next document exactly as if nothing happened; the document is still
        // written, matching design.md's "the rest of the topic is still generated" contract.
        if (FrontmatterYaml.Validate(FrontmatterYaml.Render(frontmatter), sourcePath) is { } failure)
        {
            frontmatterFailures.Add(failure);
        }

        // Written and dropped immediately: only the written path survives, for the index (AD-001).
        if (writer.Write(rendered) is { } path)
        {
            writtenPaths.Add(path);
        }
    }

    /// <summary>
    /// P1-11: the document's path relative to its service's source tree. A solution may reach into
    /// a sibling directory, in which case that path escapes the service root — such a document is
    /// nested under its owning project's name instead, so the mirrored tree can never be written
    /// outside the output directory.
    /// </summary>
    private static string RelativePathFor(ServiceDescriptor service, Project project, string documentPath)
    {
        var candidate = Path.GetRelativePath(service.RootPath, documentPath);
        if (IsContained(candidate))
        {
            return Normalize(candidate);
        }

        var projectDirectory = Path.GetDirectoryName(project.FilePath!) ?? service.RootPath;
        candidate = Path.Combine(project.Name, Path.GetRelativePath(projectDirectory, documentPath));

        return IsContained(candidate)
            ? Normalize(candidate)
            : Normalize(Path.Combine(project.Name, Path.GetFileName(documentPath)));
    }

    private static bool IsContained(string relativePath) =>
        !Path.IsPathRooted(relativePath)
        && !relativePath.Split('/', '\\').Contains("..", StringComparer.Ordinal);

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/');

    /// <summary>
    /// The solution path a service is loaded from. P1-05 requires exactly one
    /// <c>OpenSolutionAsync</c> per service and forbids an <c>OpenProjectAsync</c> loop, but a
    /// service resolved to loose projects or a manifest override has no solution file — so one is
    /// synthesized in a temp directory and deleted afterwards. design.md assigns this to
    /// <c>ServiceDiscoverer</c>; it lives here instead because the pipeline is the component that
    /// actually needs a solution path and can own the temp file's lifetime.
    /// </summary>
    private sealed class SynthesizedSolution : IDisposable
    {
        private readonly string? _directory;

        private SynthesizedSolution(string path, string? directory)
        {
            Path = path;
            _directory = directory;
        }

        public string Path { get; }

        public static SynthesizedSolution For(ServiceDescriptor service)
        {
            if (service.SolutionPath is { } existing)
            {
                return new SynthesizedSolution(existing, null);
            }

            var directory = Directory.CreateTempSubdirectory("csharp2md-sln-").FullName;
            var path = System.IO.Path.Combine(directory, service.Name.Value + ".slnx");
            var projects = service.ProjectPaths.Select(project =>
                $"  <Project Path=\"{System.IO.Path.GetFullPath(project)}\" />");

            File.WriteAllText(path, $"<Solution>\n{string.Join('\n', projects)}\n</Solution>\n");

            return new SynthesizedSolution(path, directory);
        }

        public void Dispose()
        {
            if (_directory is not null && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
