using Csharp2Md.Core.Loading;
using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Pipeline;

/// <summary>
/// Asserts the pipeline's structural invariants: one workspace at a time (P1-19), streaming writes
/// (AD-001), cancellation, and the no-output-on-invalid-manifest rule (P1-16).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AnalysisPipelineInvariantTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("csharp2md-invariant-").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_NeverHasTwoServiceWorkspacesOpenAtOnce()
    {
        var events = new List<string>();
        var loader = new SolutionLoader();

        // SolutionLoader owns its MSBuildWorkspace for exactly the duration of the call
        // (`using var workspace` inside LoadAsync, single call site), so a load that closes before
        // the next one opens is a workspace that is disposed before the next is created (P1-19).
        var pipeline = new AnalysisPipeline(async (solutionPath, cancellationToken) =>
        {
            var service = Path.GetFileNameWithoutExtension(solutionPath);
            events.Add($"open:{service}");
            try
            {
                return await loader.LoadAsync(solutionPath, cancellationToken);
            }
            finally
            {
                events.Add($"close:{service}");
            }
        });

        var manifestPath = FixtureManifest.WriteOverrides(
            _workspace, SyntheticFixtureRun.Orders, SyntheticFixtureRun.SharedContracts);

        await pipeline.RunAsync(manifestPath, Path.Combine(_workspace, "output"), CancellationToken.None);

        Assert.Equal(
            ["open:Acme.Orders", "close:Acme.Orders", "open:Acme.Shared.Contracts", "close:Acme.Shared.Contracts"],
            events);
    }

    [Fact]
    public async Task RunAsync_WritesEachServicesDocumentsBeforeOpeningTheNextWorkspace()
    {
        var outputRoot = Path.Combine(_workspace, "output");
        var loader = new SolutionLoader();
        var ordersDocumentWhenNextServiceOpened = new List<bool>();

        // AD-001: documents are written and discarded as the run streams, not accumulated into a
        // whole-codebase model and flushed at the end.
        var pipeline = new AnalysisPipeline((solutionPath, cancellationToken) =>
        {
            if (Path.GetFileNameWithoutExtension(solutionPath) == SyntheticFixtureRun.SharedContracts)
            {
                var ordersRoot = TopicLayout.ServiceRoot(outputRoot, new ServiceName(SyntheticFixtureRun.Orders));
                ordersDocumentWhenNextServiceOpened.Add(File.Exists(Path.Combine(ordersRoot, "OrderService.cs.md")));
            }

            return loader.LoadAsync(solutionPath, cancellationToken);
        });

        var manifestPath = FixtureManifest.WriteOverrides(
            _workspace, SyntheticFixtureRun.Orders, SyntheticFixtureRun.SharedContracts);

        await pipeline.RunAsync(manifestPath, outputRoot, CancellationToken.None);

        Assert.Equal([true], ordersDocumentWhenNextServiceOpened);
    }

    [Fact]
    public async Task RunAsync_InvalidManifest_ReturnsTypedFailureAndWritesNoOutput()
    {
        var manifestPath = Path.Combine(_workspace, "manifest.json");
        File.WriteAllText(manifestPath, "{ not json");
        var outputRoot = Path.Combine(_workspace, "output");

        var result = await new AnalysisPipeline().RunAsync(manifestPath, outputRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.MalformedJson, result.ManifestError!.Value.Code);
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public async Task RunAsync_MissingManifest_ReturnsTypedFailureAndWritesNoOutput()
    {
        var outputRoot = Path.Combine(_workspace, "output");

        var result = await new AnalysisPipeline()
            .RunAsync(Path.Combine(_workspace, "absent.json"), outputRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.FileMissing, result.ManifestError!.Value.Code);
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public async Task RunAsync_ZeroEntryManifest_ReturnsTypedFailureAndWritesNoOutput()
    {
        var manifestPath = FixtureManifest.Write(_workspace, new Manifest([]));
        var outputRoot = Path.Combine(_workspace, "output");

        var result = await new AnalysisPipeline().RunAsync(manifestPath, outputRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ManifestErrorCode.ZeroEntries, result.ManifestError!.Value.Code);
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public async Task RunAsync_InMemorySingleRootManifest_AnalyzesDirectoryWithoutManifestFile()
    {
        var serviceRoot = TestPaths.SyntheticSolution(SyntheticFixtureRun.SharedContracts);
        var projectPath = TestPaths.SyntheticSolution(
            Path.Combine(SyntheticFixtureRun.SharedContracts, SyntheticFixtureRun.SharedContracts + ".csproj"));
        var manifest = new Manifest(
            [new ManifestEntry(serviceRoot, SyntheticFixtureRun.SharedContracts, [projectPath])]);
        var outputRoot = Path.Combine(_workspace, "output");

        var result = await new AnalysisPipeline().RunAsync(
            manifest, serviceRoot, outputRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(outputRoot, ".csharp2md-output")));
        var serviceOutputRoot = TopicLayout.ServiceRoot(outputRoot, new ServiceName(SyntheticFixtureRun.SharedContracts));
        Assert.True(File.Exists(Path.Combine(serviceOutputRoot, "Events.cs.md")));
        Assert.Empty(Directory.EnumerateFiles(_workspace, "*.json"));
    }

    [Fact]
    public async Task RunAsync_CancelledToken_StopsBeforeLoadingAnyService()
    {
        var loads = 0;
        var pipeline = new AnalysisPipeline((_, _) =>
        {
            loads++;
            return Task.FromResult(new LoadedService(null!, new LoadReport([])));
        });

        var manifestPath = FixtureManifest.WriteOverrides(_workspace, SyntheticFixtureRun.SharedContracts);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.RunAsync(manifestPath, Path.Combine(_workspace, "output"), cancellation.Token));

        Assert.Equal(0, loads);
    }

    [Fact]
    public async Task RunAsync_DocumentOutsideTheServiceRoot_StaysInsideTheOutputTree()
    {
        // Acme.Orders resolves to its .slnx (P1-02), which T3 extended to bundle projects living in
        // sibling directories — their documents have no path *inside* the service root.
        var outputRoot = Path.Combine(_workspace, "output");
        var manifestPath = FixtureManifest.WriteRoots(_workspace, SyntheticFixtureRun.Orders);

        var result = await new AnalysisPipeline().RunAsync(manifestPath, outputRoot, CancellationToken.None);

        var codebaseRoot = TopicLayout.CodebaseRoot(outputRoot);
        var generated = Directory.EnumerateFiles(codebaseRoot, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(codebaseRoot, path).Replace('\\', '/'))
            .ToList();

        Assert.Contains("Acme.Orders/Acme.Shared.Contracts/Events.cs.md", generated);
        Assert.DoesNotContain(generated, path => path.Contains("..", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("Acme.DoesNotExist", StringComparison.Ordinal));
    }
}
