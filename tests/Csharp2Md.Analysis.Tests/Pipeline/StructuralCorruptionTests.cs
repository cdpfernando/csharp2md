using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class StructuralCorruptionTests
{
    [Fact]
    [Trait("Requirement", "ENG-24")]
    [Trait("Requirement", "ROSE-59")]
    public async Task AnalyzeAsync_ValidationReportsStructuralCorruption_AbortsAndKeepsPriorPublication()
    {
        var store = new InMemoryTransactionalStore();
        var solutionPath = "alpha.sln";
        var sessionKey = Path.GetFullPath(solutionPath);
        var request = AnalysisRequest.Create([solutionPath]);

        var first = await new AnalysisEngine(store, StubStages.CreateDefault()).AnalyzeAsync(request, CancellationToken.None);

        var firstOutcome = Assert.Single(first.Solutions);
        Assert.Equal(PublicationStatus.Committed, firstOutcome.Status);
        Assert.False(firstOutcome.StructuralCorruption);
        Assert.True(store.TryGetPublication(sessionKey, out var prior));
        Assert.Equal(sessionKey, prior.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, prior.ArtifactsInPublicationOrder[^1].Role);

        var executed = new List<string>();
        var corruptedValidation = new ResultStage(
            "Validation and Coverage",
            new StageResult(0, 0, 0, StructuralCorruption: true, HasUnknownsOrCandidatesOrFrontiers: false),
            executed);
        var stages = StubStages.CreateDefault()
            .SetItem(4, corruptedValidation)
            .SetItem(5, new RecordingStage("Persistence", executed));

        var second = await new AnalysisEngine(store, stages).AnalyzeAsync(request, CancellationToken.None);

        var outcome = Assert.Single(second.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.True(outcome.StructuralCorruption);
        Assert.False(outcome.HasUnknownsOrCandidatesOrFrontiers);
        Assert.Null(outcome.FailingStage);
        Assert.True(second.HasUnpublishedSolution);
        Assert.Equal(["Validation and Coverage"], executed);
        Assert.DoesNotContain("Persistence", outcome.Stages.Select(report => report.Name));
        Assert.Equal("Validation and Coverage", outcome.Stages[^1].Name);

        Assert.True(store.TryGetPublication(sessionKey, out var kept));
        Assert.Equal(prior, kept);
        Assert.Equal(prior.SolutionKey, kept.SolutionKey);
        Assert.Equal(prior.ArtifactsInPublicationOrder.Length, kept.ArtifactsInPublicationOrder.Length);
        for (var index = 0; index < prior.ArtifactsInPublicationOrder.Length; index++)
        {
            Assert.Equal(prior.ArtifactsInPublicationOrder[index].Role, kept.ArtifactsInPublicationOrder[index].Role);
            Assert.Equal(
                prior.ArtifactsInPublicationOrder[index].CanonicalKey,
                kept.ArtifactsInPublicationOrder[index].CanonicalKey);
            Assert.True(
                prior.ArtifactsInPublicationOrder[index].Payload.AsSpan()
                    .SequenceEqual(kept.ArtifactsInPublicationOrder[index].Payload.AsSpan()));
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-31")]
    [Trait("Requirement", "ROSE-59")]
    public async Task AnalyzeAsync_PublicationRejected_MarksUnpublishedKeepsPriorBytes()
    {
        var inner = new InMemoryTransactionalStore();
        var solutionPath = "alpha.sln";
        var sessionKey = Path.GetFullPath(solutionPath);
        var request = AnalysisRequest.Create([solutionPath]);

        var first = await new AnalysisEngine(new RejectingCommitStore(inner, rejectCommit: false), StubStages.CreateDefault())
            .AnalyzeAsync(request, CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(first.Solutions).Status);
        Assert.True(inner.TryGetPublication(sessionKey, out var prior));
        var priorBytes = prior.ArtifactsInPublicationOrder
            .Select(fragment => (fragment.Role, fragment.CanonicalKey, Payload: fragment.Payload.ToArray()))
            .ToArray();

        var rejecting = new RejectingCommitStore(inner, rejectCommit: true);
        var second = await new AnalysisEngine(rejecting, StubStages.CreateDefault()).AnalyzeAsync(request, CancellationToken.None);

        var outcome = Assert.Single(second.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.True(outcome.StructuralCorruption);
        Assert.Null(outcome.FailingStage);
        Assert.Equal("schema: facts/structural.json", outcome.Detail);
        Assert.True(second.HasUnpublishedSolution);
        Assert.Equal(1, rejecting.AbortCount);

        Assert.True(inner.TryGetPublication(sessionKey, out var kept));
        Assert.Equal(prior.SolutionKey, kept.SolutionKey);
        Assert.Equal(priorBytes.Length, kept.ArtifactsInPublicationOrder.Length);
        for (var index = 0; index < priorBytes.Length; index++)
        {
            Assert.Equal(priorBytes[index].Role, kept.ArtifactsInPublicationOrder[index].Role);
            Assert.Equal(priorBytes[index].CanonicalKey, kept.ArtifactsInPublicationOrder[index].CanonicalKey);
            Assert.True(
                priorBytes[index].Payload.AsSpan()
                    .SequenceEqual(kept.ArtifactsInPublicationOrder[index].Payload.AsSpan()));
        }

        Assert.Equal(ArtifactRole.Manifest, kept.ArtifactsInPublicationOrder[^1].Role);
        Assert.Equal(prior.ArtifactsInPublicationOrder[^1].CanonicalKey, kept.ArtifactsInPublicationOrder[^1].CanonicalKey);
    }
}

internal sealed class RejectingCommitStore : ITransactionalStore
{
    private readonly InMemoryTransactionalStore _inner;
    private readonly bool _rejectCommit;

    public RejectingCommitStore(InMemoryTransactionalStore inner, bool rejectCommit)
    {
        _inner = inner;
        _rejectCommit = rejectCommit;
    }

    public int AbortCount { get; private set; }

    public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader) => new Session(_inner.Open(solutionKey, sourceReader), this);

    private sealed class Session : IStoreSession
    {
        private readonly IStoreSession _inner;
        private readonly RejectingCommitStore _store;

        public Session(IStoreSession inner, RejectingCommitStore store)
        {
            _inner = inner;
            _store = store;
        }

        public void Stage(FactualSnapshot snapshot) => _inner.Stage(snapshot);

        public CommittedPublication Commit()
        {
            if (_store._rejectCommit)
            {
                throw new PublicationRejectedException("schema", "facts/structural.json");
            }

            return _inner.Commit();
        }

        public void Abort()
        {
            _store.AbortCount++;
            _inner.Abort();
        }
    }
}
