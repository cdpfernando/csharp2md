using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Storage;

public sealed class InMemoryTransactionalStore : ITransactionalStore
{
    public IStoreSession Open(string solutionKey) => new Session(solutionKey);

    private sealed class Session : IStoreSession
    {
        private readonly string _solutionKey;
        private readonly List<StagedFragment> _staged = [];
        private bool _committed;

        public Session(string solutionKey) => _solutionKey = solutionKey;

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

            return new CommittedPublication(_solutionKey, ordered);
        }

        public void Abort() => _staged.Clear();
    }
}
