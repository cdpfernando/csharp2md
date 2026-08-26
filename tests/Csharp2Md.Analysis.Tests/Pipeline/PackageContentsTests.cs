using Csharp2Md.Analysis;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PackageContentsTests
{
    [Fact]
    [Trait("Requirement", "ROSE-57")]
    public async Task AnalyzeAsync_CommittedPublication_HasNoMarkdownCatalogsPostingsOrSourceProjection()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var keys = publication.ArtifactsInPublicationOrder.Select(fragment => fragment.CanonicalKey).ToArray();
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.False(key.EndsWith(".md", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            keys,
            key => key.Equals("catalogs", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("catalogs/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            keys,
            key => key.Equals("postings", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("postings/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            keys,
            key => key.Equals("source-projection", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("source-projection/", StringComparison.OrdinalIgnoreCase));
    }
}
