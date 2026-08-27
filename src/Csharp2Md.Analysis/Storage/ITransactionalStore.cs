namespace Csharp2Md.Analysis.Storage;

public interface ITransactionalStore
{
    IStoreSession Open(SolutionCoordinate coordinate, ISourceDocumentReader sourceReader) =>
        Open(coordinate.Identity.Value, sourceReader);

    IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader) =>
        Open(SolutionCoordinate.For(solutionKey), sourceReader);
}

public interface IStoreSession
{
    void Stage(FactualSnapshot snapshot);

    CommittedPublication Commit();

    void Abort();
}
