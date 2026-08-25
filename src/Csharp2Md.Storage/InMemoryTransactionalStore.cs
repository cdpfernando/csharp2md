using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;

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

            var document = DomainMapper.ToWire(_staged, new ManifestContext(_solutionKey, SolutionFileName(_solutionKey)));
            var report = PackageValidator.Validate(document);
            var artifacts = PackagePublisher.ToPublicationOrder(report.Document);

            _committed = true;
            var publication = new CommittedPublication(_solutionKey, artifacts);
            _store.Publish(publication);
            return publication;
        }

        public void Abort() => _staged = FactualSnapshot.Empty;
    }

    private static string SolutionFileName(string solutionKey)
    {
        var separator = solutionKey.LastIndexOfAny(['/', '\\']);
        return separator < 0 ? solutionKey : solutionKey[(separator + 1)..];
    }
}
