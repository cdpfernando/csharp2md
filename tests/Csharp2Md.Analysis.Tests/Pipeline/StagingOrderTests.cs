using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class StagingOrderTests
{
    [Fact]
    [Trait("Requirement", "ENG-27")]
    public async Task AnalyzeAsync_TwoPersistenceStagingOrders_CommitIdenticalArtifacts()
    {
        var solutionPath = "alpha.sln";
        var sessionKey = Path.GetFullPath(solutionPath);

        var first = await PublishWithStagingOrder(solutionPath, FactualSnapshot.Empty, FactualSnapshot.Empty);
        var second = await PublishWithStagingOrder(solutionPath, FactualSnapshot.Empty, FactualSnapshot.Empty);

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);
        Assert.Equal(sessionKey, first.Publication.SolutionKey);
        Assert.Equal(sessionKey, second.Publication.SolutionKey);

        AssertEqualArtifacts(first.Publication.ArtifactsInPublicationOrder, second.Publication.ArtifactsInPublicationOrder);
        Assert.Equal(ArtifactRole.Manifest, first.Publication.ArtifactsInPublicationOrder[^1].Role);
        Assert.Equal(ArtifactRole.Manifest, second.Publication.ArtifactsInPublicationOrder[^1].Role);
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> PublishWithStagingOrder(
        string solutionPath,
        params FactualSnapshot[] snapshots)
    {
        var store = new InMemoryTransactionalStore();
        var persistence = new StagingPersistence(snapshots);
        var engine = new AnalysisEngine(store, StubStages.CreateDefault().SetItem(5, persistence));

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static void AssertEqualArtifacts(
        ImmutableArray<StagedFragment> left,
        ImmutableArray<StagedFragment> right)
    {
        Assert.Equal(left.Length, right.Length);
        for (var index = 0; index < left.Length; index++)
        {
            Assert.Equal(left[index].Role, right[index].Role);
            Assert.Equal(left[index].CanonicalKey, right[index].CanonicalKey);
            Assert.True(
                left[index].Payload.AsSpan().SequenceEqual(right[index].Payload.AsSpan()),
                $"Payload bytes at index {index} differ.");
        }
    }
}

internal sealed class StagingPersistence : IPipelineStage
{
    private readonly ImmutableArray<FactualSnapshot> _snapshots;

    public StagingPersistence(params FactualSnapshot[] snapshots) => _snapshots = [.. snapshots];

    public string Name => "Persistence";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        foreach (var snapshot in _snapshots)
        {
            context.Session.Stage(snapshot);
        }

        return StubStages.ZeroResult();
    }
}
