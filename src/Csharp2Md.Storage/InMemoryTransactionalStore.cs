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
        private readonly List<StagedFragment> _staged = [];
        private bool _committed;

        public Session(InMemoryTransactionalStore store, string solutionKey)
        {
            _store = store;
            _solutionKey = solutionKey;
        }

        public void Stage(StagedFragment fragment) => _staged.Add(fragment);

        public CommittedPublication Commit()
        {
            if (_committed)
            {
                throw new InvalidOperationException("A store session commits exactly once.");
            }

            _committed = true;

            var ordered = _staged
                .Where(fragment => fragment.Role == ArtifactRole.Payload)
                .OrderBy(fragment => fragment.CanonicalKey, StringComparer.Ordinal)
                .Concat(_staged
                    .Where(fragment => fragment.Role == ArtifactRole.Manifest)
                    .OrderBy(fragment => fragment.CanonicalKey, StringComparer.Ordinal))
                .ToImmutableArray();

            var publication = new CommittedPublication(_solutionKey, ordered);
            _store.Publish(publication);
            return publication;
        }

        public void Abort() => _staged.Clear();
    }
}
