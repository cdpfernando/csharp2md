using Csharp2Md.Core.Graph;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Tests.Pipeline;

namespace Csharp2Md.Core.Tests.Cli;

/// <summary>
/// Runs the packaged CLI apphost against the synthetic fixture and makes spec.md's P1 and P2
/// Independent Tests executable (T26's own "Done when" requirement), plus the P1-16 and BuildHost
/// error-handling paths from design.md's Error Handling Strategy.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EndToEndTests : IAsyncLifetime
{
    public Task InitializeAsync() => CliBinary.EnsureBuiltAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Run_AgainstFixture_ExitsZeroAndWritesFullArtifactSet()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteOverrides(
                workspace, SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts);
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath, $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot, CancellationToken.None);

            Assert.True(result.ExitCode == 0, $"run failed (exit {result.ExitCode}):\n{result.StandardOutput}\n{result.StandardError}");

            // P1-11: one .md per .cs, mirroring the source tree.
            var ordersRoot = TestPaths.SyntheticSolution(SyntheticFixtureRun.Orders);
            var expectedDocs = FixtureManifest.ExpectedSourceFiles(ordersRoot).Select(source => source + ".md");
            var generatedDocs = Directory
                .EnumerateFiles(Path.Combine(outputPath, SyntheticFixtureRun.Orders), "*.md", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Path.Combine(outputPath, SyntheticFixtureRun.Orders), path).Replace('\\', '/'))
                .Where(relative => relative != IndexWriter.FileName);
            Assert.Equal(expectedDocs.OrderBy(x => x, StringComparer.Ordinal), generatedDocs.OrderBy(x => x, StringComparer.Ordinal));

            // P1-13 / P1-14: per-service and root index.
            Assert.True(File.Exists(Path.Combine(outputPath, SyntheticFixtureRun.Orders, IndexWriter.FileName)));
            Assert.True(File.Exists(Path.Combine(outputPath, IndexWriter.FileName)));

            // P2-11 / P2-13: the consolidated graph artifacts.
            Assert.True(File.Exists(Path.Combine(outputPath, DependencyJsonWriter.FileName)));
            Assert.True(File.Exists(Path.Combine(outputPath, MermaidWriter.FileName)));

            // P1-10: the console summary.
            Assert.Contains("Run summary:", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_AgainstFixture_DependenciesJsonMatchesKnownFixtureDependencies()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteOverrides(
                workspace, SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts);
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath, $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot, CancellationToken.None);
            Assert.True(result.ExitCode == 0, $"run failed (exit {result.ExitCode}):\n{result.StandardOutput}\n{result.StandardError}");

            var graph = DependencyJsonWriter.Deserialize(
                File.ReadAllText(Path.Combine(outputPath, DependencyJsonWriter.FileName)));

            // P2-01/P2-07 — HTTP call to a hard-coded appsettings entry.
            AssertEdge(graph, "Acme.Orders", "PaymentService", CommunicationType.SincronoBloqueante, ResolutionKind.HardCoded);

            // P2-02/P2-09 — gRPC unary call whose target ("Payments", the proto service name) has no
            // config entry, per AD-005 recorded as Unresolved rather than matched to a catalog service.
            AssertEdge(graph, "Acme.Orders", "Payments", CommunicationType.SincronoBloqueante, ResolutionKind.Unresolved);

            // P2-14 — a publish in Acme.Orders correlated with a subscribe in Acme.Payments (OrderPlaced).
            AssertEdge(graph, "Acme.Orders", "Acme.Payments", CommunicationType.PubSubEvento, ResolutionKind.NotApplicable);

            // P2-15 — Acme.Payments' PaymentProcessed publish has no subscriber anywhere in the
            // fixture; retained as an edge targeting the topic name, not dropped.
            AssertEdge(graph, "Acme.Payments", "PaymentProcessed", CommunicationType.PubSubEvento, ResolutionKind.Unresolved);

            // P2-04 — internal project references to the shared contracts library.
            AssertEdge(graph, "Acme.Orders", "Acme.Shared.Contracts", CommunicationType.DirectReference, ResolutionKind.NotApplicable);
            AssertEdge(graph, "Acme.Payments", "Acme.Shared.Contracts", CommunicationType.DirectReference, ResolutionKind.NotApplicable);

            // P2-12 — every edge carries one of exactly the five defined communication types.
            Assert.All(graph.Edges, edge => Assert.True(Enum.IsDefined(edge.Communication)));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_InvalidManifest_ExitsNonZeroAndWritesNoOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-invalid-").FullName;
        try
        {
            var manifestPath = Path.Combine(workspace, "manifest.json");
            File.WriteAllText(manifestPath, "{ not valid json");
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath, $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot, CancellationToken.None);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("csharp2md:", result.StandardError, StringComparison.Ordinal); // P1-16: names the problem
            Assert.False(Directory.Exists(outputPath) && Directory.EnumerateFileSystemEntries(outputPath).Any());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_CannotResolveDotnet_PrintsActionableMessageNotRawException()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-nodotnet-").FullName;

        // A PATH with no `dotnet` on it — the BuildHost launches `dotnet` out of process
        // (design.md), so this is the only way to genuinely reproduce a machine that can't
        // resolve it, per ProcessRunner's own environment-override contract.
        var pathWithoutDotnet = Directory.CreateTempSubdirectory("csharp2md-empty-path-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteOverrides(workspace, SyntheticFixtureRun.Orders);
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath,
                $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot,
                CancellationToken.None,
                environment: new Dictionary<string, string> { ["PATH"] = pathWithoutDotnet, ["DOTNET_ROOT"] = pathWithoutDotnet });

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("csharp2md: could not start the Roslyn build host", result.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("Unhandled exception", result.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("   at ", result.StandardError, StringComparison.Ordinal); // no raw stack trace
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
            Directory.Delete(pathWithoutDotnet, recursive: true);
        }
    }

    private static void AssertEdge(
        DependencyGraph graph, string source, string target, CommunicationType communication, ResolutionKind resolution) =>
        Assert.Contains(
            graph.Edges,
            edge => edge.Source.Value == source
                && edge.Target.Value == target
                && edge.Communication == communication
                && edge.Resolution == resolution);
}
