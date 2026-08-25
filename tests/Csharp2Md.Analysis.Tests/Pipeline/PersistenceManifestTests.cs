using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PersistenceManifestTests
{
    [Fact]
    [Trait("Requirement", "ENG-23")]
    public async Task AnalyzeAsync_CommittedPublication_EndsWithAManifestFragment()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store);

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);

        var sessionKey = Path.GetFullPath(solutionPath);
        Assert.True(store.TryGetPublication(sessionKey, out var publication));
        Assert.Equal(sessionKey, publication.SolutionKey);
        Assert.NotEmpty(publication.ArtifactsInPublicationOrder);
        Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role);
    }
}
