using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class CommitOnceTests
{
    [Fact]
    [Trait("Requirement", "ENG-22")]
    [Trait("Requirement", "ROSE-59")]
    public async Task AnalyzeAsync_SuccessfulSolution_CommitsTheSessionExactlyOnce()
    {
        var inner = new InMemoryTransactionalStore();
        var store = new CountingStore(inner);
        var engine = new AnalysisEngine(store, StubStages.CreateDefault());
        var solutionPath = "alpha.sln";

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.Equal(1, store.CommitCount);
        Assert.Equal(0, store.AbortCount);
        Assert.False(result.HasUnpublishedSolution);

        var sessionKey = Path.GetFullPath(solutionPath);
        Assert.True(inner.TryGetPublication(sessionKey, out var publication));
        Assert.Equal(sessionKey, publication.SolutionKey);
        Assert.NotEmpty(publication.ArtifactsInPublicationOrder);
        Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role);
        Assert.Contains(
            publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey == "contracts/taxonomy-registry.json");
    }
}

internal sealed class CountingStore : ITransactionalStore
{
    private readonly ITransactionalStore _inner;

    public CountingStore(ITransactionalStore inner) => _inner = inner;

    public int CommitCount { get; private set; }

    public int AbortCount { get; private set; }

    public IStoreSession Open(string solutionKey) => new Session(_inner.Open(solutionKey), this);

    private sealed class Session : IStoreSession
    {
        private readonly IStoreSession _inner;
        private readonly CountingStore _store;

        public Session(IStoreSession inner, CountingStore store)
        {
            _inner = inner;
            _store = store;
        }

        public void Stage(FactualSnapshot snapshot) => _inner.Stage(snapshot);

        public CommittedPublication Commit()
        {
            _store.CommitCount++;
            return _inner.Commit();
        }

        public void Abort()
        {
            _store.AbortCount++;
            _inner.Abort();
        }
    }
}
