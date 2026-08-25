using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class UnknownsCommitTests
{
    [Fact]
    [Trait("Requirement", "ENG-26")]
    public async Task AnalyzeAsync_UnknownsWithoutCorruption_CommitsAndExposesTheFlag()
    {
        var inner = new InMemoryTransactionalStore();
        var store = new CountingStore(inner);
        var unknowns = new ResultStage(
            "Validation and Coverage",
            new StageResult(0, 0, 0, StructuralCorruption: false, HasUnknownsOrCandidatesOrFrontiers: true));
        var engine = new AnalysisEngine(store, StubStages.CreateDefault().SetItem(4, unknowns));
        var solutionPath = "alpha.sln";

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(outcome.HasUnknownsOrCandidatesOrFrontiers);
        Assert.False(outcome.StructuralCorruption);
        Assert.Null(outcome.FailingStage);
        Assert.False(result.HasUnpublishedSolution);
        Assert.Equal(1, store.CommitCount);
        Assert.Equal(0, store.AbortCount);

        var sessionKey = Path.GetFullPath(solutionPath);
        Assert.True(inner.TryGetPublication(sessionKey, out var publication));
        Assert.Equal(sessionKey, publication.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, Assert.Single(publication.ArtifactsInPublicationOrder).Role);
    }
}
