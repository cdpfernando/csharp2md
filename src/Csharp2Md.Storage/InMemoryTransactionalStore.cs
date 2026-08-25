using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Storage;

public sealed class InMemoryTransactionalStore : ITransactionalStore
{
    public IStoreSession Open(string solutionKey) => new Session(solutionKey);

    private sealed class Session : IStoreSession
    {
        private readonly string _solutionKey;
        private readonly List<StagedFragment> _staged = [];

        public Session(string solutionKey) => _solutionKey = solutionKey;

        public void Stage(StagedFragment fragment) => _staged.Add(fragment);

        public CommittedPublication Commit() =>
            new(_solutionKey, _staged.ToImmutableArray());

        public void Abort() => _staged.Clear();
    }
}
