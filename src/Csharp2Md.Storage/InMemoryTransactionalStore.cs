using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage;

public sealed class InMemoryTransactionalStore : ITransactionalStore
{
    private readonly Dictionary<string, CommittedPublication> _publications = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeKeys = new(StringComparer.Ordinal);
    private readonly IPackageProjector? _projector;

    public InMemoryTransactionalStore(IPackageProjector? projector = null)
    {
        _projector = projector;
    }

    public IStoreSession Open(SolutionCoordinate coordinate, ISourceDocumentReader sourceReader)
    {
        ArgumentNullException.ThrowIfNull(sourceReader);
        return Open(coordinate.Identity.Value, sourceReader);
    }

    public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader)
    {
        ArgumentException.ThrowIfNullOrEmpty(solutionKey);
        ArgumentNullException.ThrowIfNull(sourceReader);
        if (!_activeKeys.Add(solutionKey))
        {
            throw new PublicationRejectedException("lock", solutionKey);
        }

        return new Session(this, solutionKey, sourceReader, _projector);
    }

    public bool TryGetPublication(string solutionKey, out CommittedPublication publication) =>
        _publications.TryGetValue(solutionKey, out publication!);

    private void Release(string solutionKey) => _activeKeys.Remove(solutionKey);

    private void Publish(CommittedPublication publication) =>
        _publications[publication.SolutionKey] = publication;

    private sealed class Session : IStoreSession, IDeferredFragmentStaging
    {
        private readonly InMemoryTransactionalStore _store;
        private readonly string _solutionKey;
        private readonly ISourceDocumentReader _sourceReader;
        private readonly IPackageProjector? _projector;
        private readonly List<StagedFragment> _deferred = [];
        private FactualSnapshot _staged = FactualSnapshot.Empty;
        private bool _committed;

        public Session(
            InMemoryTransactionalStore store,
            string solutionKey,
            ISourceDocumentReader sourceReader,
            IPackageProjector? projector)
        {
            _store = store;
            _solutionKey = solutionKey;
            _sourceReader = sourceReader;
            _projector = projector;
        }

        public void Stage(FactualSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            EnsureActive();
            _staged = _staged.Merge(snapshot);
        }

        public void StageDeferred(StagedFragment fragment)
        {
            ArgumentNullException.ThrowIfNull(fragment);
            EnsureActive();
            _deferred.Add(fragment);
        }

        public CommittedPublication Commit()
        {
            EnsureActive();
            _ = _sourceReader.Documents;

            var artifacts = PublicationPipeline.Publish(
                _staged,
                new ManifestContext(_solutionKey, SolutionFileName(_solutionKey)),
                _projector,
                _sourceReader);
            artifacts = artifacts.AddRange(_deferred);
            foreach (var fragment in _deferred)
            {
                _ = fragment.ReadPayload();
            }

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
