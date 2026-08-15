using Csharp2Md.Core.Graph;
using Csharp2Md.Core.Loading;
using Csharp2Md.Core.Output;

namespace Csharp2Md.Core.Tests.Pipeline;

/// <summary>
/// Asserts the artifact set one full run produces against the synthetic fixture (T24's
/// "integration test runs the full pipeline against the fixture" criterion).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AnalysisPipelineTests(SyntheticFixtureRun run) : IClassFixture<SyntheticFixtureRun>
{
    [Fact]
    public void RunAsync_ProducesExactlyOneMarkdownFilePerSourceDocument()
    {
        var serviceRoot = TestPaths.SyntheticSolution(SyntheticFixtureRun.Orders);
        var expected = FixtureManifest.ExpectedSourceFiles(serviceRoot)
            .Select(source => source + ".md")
            .ToList();

        Assert.Equal(expected, GeneratedDocuments(run.ServiceOutput(SyntheticFixtureRun.Orders)));
    }

    [Fact]
    public void RunAsync_MirrorsSourceStructureAndExcludesBuildOutput()
    {
        var generated = GeneratedDocuments(run.ServiceOutput(SyntheticFixtureRun.Orders));

        Assert.Contains("OrderService.cs.md", generated);
        Assert.DoesNotContain(generated, path => path.Contains("obj/", StringComparison.Ordinal));
        Assert.DoesNotContain(generated, path => path.EndsWith(".g.cs.md", StringComparison.Ordinal));
    }

    [Fact]
    public void RunAsync_WritesPerServiceIndexLinkingEveryGeneratedFile()
    {
        var indexPath = Path.Combine(run.ServiceOutput(SyntheticFixtureRun.Orders), IndexWriter.FileName);

        var index = File.ReadAllText(indexPath);

        Assert.Contains("# Acme.Orders", index, StringComparison.Ordinal);
        Assert.Contains("[OrderService.cs](./OrderService.cs.md)", index, StringComparison.Ordinal);
    }

    [Fact]
    public void RunAsync_WritesRootIndexLinkingEveryPerServiceIndex()
    {
        var index = File.ReadAllText(Path.Combine(run.OutputRoot, IndexWriter.FileName));

        Assert.Contains("[Acme.Orders](./Acme.Orders/index.md)", index, StringComparison.Ordinal);
        Assert.Contains("[Acme.Payments](./Acme.Payments/index.md)", index, StringComparison.Ordinal);
        Assert.Contains(
            "[Acme.Shared.Contracts](./Acme.Shared.Contracts/index.md)", index, StringComparison.Ordinal);
    }

    [Fact]
    public void RunAsync_WritesDependencyJsonAndMermaidDiagramFromTheSameGraph()
    {
        var jsonPath = Path.Combine(run.OutputRoot, DependencyJsonWriter.FileName);
        var mermaidPath = Path.Combine(run.OutputRoot, MermaidWriter.FileName);

        var deserialized = DependencyJsonWriter.Deserialize(File.ReadAllText(jsonPath));
        var mermaid = File.ReadAllText(mermaidPath);

        Assert.Equal(run.Result.Graph.Edges.Count, deserialized.Edges.Count);
        Assert.StartsWith("graph LR", mermaid, StringComparison.Ordinal);
        Assert.All(
            run.Result.Graph.Edges,
            edge => Assert.Contains(MermaidWriter.Label(edge.Communication), mermaid, StringComparison.Ordinal));
    }

    [Fact]
    public void RunAsync_UnrestoredProject_IsReportedAsPossibleMissingRestore()
    {
        var payments = Assert.Single(
            run.Result.LoadReport.Projects, project => project.ProjectName == SyntheticFixtureRun.Payments);

        Assert.Equal(ProjectLoadStatus.PossibleMissingRestore, payments.Status);
    }

    [Fact]
    public void RunAsync_DegradedProject_DoesNotStopTheRunFromCompleting()
    {
        Assert.True(run.Result.IsSuccess);
        Assert.Contains(
            run.Result.LoadReport.Projects,
            project => project.Status is ProjectLoadStatus.PossibleMissingRestore);
        Assert.Contains(run.Result.LoadReport.Projects, project => project.Status is ProjectLoadStatus.Ok);
    }

    [Fact]
    public void RunAsync_DetectsAgainstTheCompleteCatalog_NotOnlyServicesSeenSoFar()
    {
        // Acme.Shared.Contracts is the last manifest entry, so this edge exists only if Stage 1
        // finished for every service before the first detector ran.
        Assert.Contains(
            run.Result.Graph.Edges,
            edge => edge.Source == new ServiceName(SyntheticFixtureRun.Orders)
                && edge.Target == new ServiceName(SyntheticFixtureRun.SharedContracts)
                && edge.Communication == CommunicationType.DirectReference);
    }

    private static IReadOnlyList<string> GeneratedDocuments(string serviceOutputRoot) =>
        Directory.EnumerateFiles(serviceOutputRoot, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(serviceOutputRoot, path).Replace('\\', '/'))
            .Where(relative => relative != IndexWriter.FileName)
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .ToList();
}
