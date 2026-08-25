using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Storage;

public sealed class InMemoryTransactionalStore : ITransactionalStore
{
    private readonly Dictionary<string, CommittedPublication> _publications = new(StringComparer.Ordinal);

    public IStoreSession Open(string solutionKey) => new Session(this, solutionKey);

    public bool TryGetPublication(string solutionKey, out CommittedPublication publication) =>
        _publications.TryGetValue(solutionKey, out publication!);

    private void Publish(CommittedPublication publication) =>
        _publications[publication.SolutionKey] = publication;

    private sealed class Session : IStoreSession
    {
        private readonly InMemoryTransactionalStore _store;
        private readonly string _solutionKey;
        private FactualSnapshot _staged = FactualSnapshot.Empty;
        private bool _committed;

        public Session(InMemoryTransactionalStore store, string solutionKey)
        {
            _store = store;
            _solutionKey = solutionKey;
        }

        public void Stage(FactualSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _staged = _staged.Merge(snapshot);
        }

        public CommittedPublication Commit()
        {
            if (_committed)
            {
                throw new InvalidOperationException("A store session commits exactly once.");
            }

            _committed = true;
            _ = _staged;

            ImmutableArray<StagedFragment> ordered =
            [
                new StagedFragment(ArtifactRole.Manifest, "manifest", []),
            ];

            var publication = new CommittedPublication(_solutionKey, ordered);
            _store.Publish(publication);
            return publication;
        }

        public void Abort() => _staged = FactualSnapshot.Empty;
    }
}
