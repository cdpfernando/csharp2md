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
        var alpha = new StagedFragment(ArtifactRole.Payload, "alpha", [0x41]);
        var zeta = new StagedFragment(ArtifactRole.Payload, "zeta", [0x5A]);
        var manifest = new StagedFragment(ArtifactRole.Manifest, "manifest", [0x4D]);
        var solutionPath = "alpha.sln";
        var sessionKey = Path.GetFullPath(solutionPath);

        var first = await PublishWithStagingOrder(solutionPath, zeta, alpha, manifest);
        var second = await PublishWithStagingOrder(solutionPath, manifest, alpha, zeta);

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);
        Assert.Equal(sessionKey, first.Publication.SolutionKey);
        Assert.Equal(sessionKey, second.Publication.SolutionKey);

        AssertEqualArtifacts(first.Publication.ArtifactsInPublicationOrder, second.Publication.ArtifactsInPublicationOrder);
        Assert.Equal(3, first.Publication.ArtifactsInPublicationOrder.Length);
        AssertFragment(first.Publication.ArtifactsInPublicationOrder[0], ArtifactRole.Payload, "alpha", [0x41]);
        AssertFragment(first.Publication.ArtifactsInPublicationOrder[1], ArtifactRole.Payload, "zeta", [0x5A]);
        AssertFragment(first.Publication.ArtifactsInPublicationOrder[2], ArtifactRole.Manifest, "manifest", [0x4D]);
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> PublishWithStagingOrder(
        string solutionPath,
        params StagedFragment[] fragments)
    {
        var store = new InMemoryTransactionalStore();
        var persistence = new StagingPersistence(fragments);
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

    private static void AssertFragment(
        StagedFragment fragment,
        ArtifactRole role,
        string canonicalKey,
        ReadOnlySpan<byte> payload)
    {
        Assert.Equal(role, fragment.Role);
        Assert.Equal(canonicalKey, fragment.CanonicalKey);
        Assert.True(fragment.Payload.AsSpan().SequenceEqual(payload));
    }
}

internal sealed class StagingPersistence : IPipelineStage
{
    private readonly ImmutableArray<StagedFragment> _fragments;

    public StagingPersistence(params StagedFragment[] fragments) => _fragments = [.. fragments];

    public string Name => "Persistence";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        foreach (var fragment in _fragments)
        {
            context.Session.Stage(fragment);
        }

        return StubStages.ZeroResult();
    }
}
