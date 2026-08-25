namespace Csharp2Md.Analysis.Storage;

public interface ITransactionalStore
{
    IStoreSession Open(string solutionKey);
}

public interface IStoreSession
{
    void Stage(StagedFragment fragment);

    CommittedPublication Commit();

    void Abort();
}
