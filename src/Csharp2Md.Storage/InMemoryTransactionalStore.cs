using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage;

public sealed class InMemoryTransactionalStore : ITransactionalStore
{
    private readonly Dictionary<string, CommittedPublication> _publications = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeKeys = new(StringComparer.Ordinal);

    public IStoreSession Open(string solutionKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(solutionKey);
        if (!_activeKeys.Add(solutionKey))
        {
            throw new PublicationRejectedException("lock", solutionKey);
        }

        return new Session(this, solutionKey);
    }

    public bool TryGetPublication(string solutionKey, out CommittedPublication publication) =>
        _publications.TryGetValue(solutionKey, out publication!);

    private void Release(string solutionKey) => _activeKeys.Remove(solutionKey);

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
            EnsureActive();
            _staged = _staged.Merge(snapshot);
        }

        public CommittedPublication Commit()
        {
            EnsureActive();

            var artifacts = PublicationPipeline.Publish(
                _staged,
                new ManifestContext(_solutionKey, SolutionFileName(_solutionKey)));

            _committed = true;
            _store.Release(_solutionKey);
            var publication = new CommittedPublication(_solutionKey, artifacts);
            _store.Publish(publication);
            return publication;
        }

        public void Abort()
        {
            _staged = FactualSnapshot.Empty;
            _store.Release(_solutionKey);
        }

        private void EnsureActive()
        {
            if (_committed)
            {
                throw new PublicationRejectedException("session-state", _solutionKey);
            }
        }
    }

    private static string SolutionFileName(string solutionKey)
    {
        var separator = solutionKey.LastIndexOfAny(['/', '\\']);
        return separator < 0 ? solutionKey : solutionKey[(separator + 1)..];
    }
}
