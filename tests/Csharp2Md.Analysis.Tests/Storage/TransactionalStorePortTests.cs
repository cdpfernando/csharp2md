using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class TransactionalStorePortTests
{
    [Fact]
    [Trait("Requirement", "ENG-20")]
    [Trait("Requirement", "MSC-02")]
    public void ITransactionalStore_Open_TakesSolutionCoordinateAndAStringOverload()
    {
        var coordinateOpen = typeof(ITransactionalStore).GetMethod(
            nameof(ITransactionalStore.Open),
            [typeof(SolutionCoordinate), typeof(ISourceDocumentReader)]);
        var stringOpen = typeof(ITransactionalStore).GetMethod(
            nameof(ITransactionalStore.Open),
            [typeof(string), typeof(ISourceDocumentReader)]);

        Assert.NotNull(coordinateOpen);
        Assert.NotNull(stringOpen);
        Assert.Equal(typeof(IStoreSession), coordinateOpen.ReturnType);
        Assert.Equal(typeof(IStoreSession), stringOpen.ReturnType);

        var coordinateParameters = coordinateOpen.GetParameters();
        Assert.Equal(typeof(SolutionCoordinate), coordinateParameters[0].ParameterType);
        Assert.Equal("coordinate", coordinateParameters[0].Name);
        Assert.Equal(typeof(ISourceDocumentReader), coordinateParameters[1].ParameterType);
        Assert.Equal("sourceReader", coordinateParameters[1].Name);

        var stringParameters = stringOpen.GetParameters();
        Assert.Equal(typeof(string), stringParameters[0].ParameterType);
        Assert.Equal("solutionKey", stringParameters[0].Name);
        Assert.Equal(typeof(ISourceDocumentReader), stringParameters[1].ParameterType);
        Assert.Equal("sourceReader", stringParameters[1].Name);
    }

    [Fact]
    [Trait("Requirement", "MSC-02")]
    public void Open_StringOverload_DelegatesThroughSolutionCoordinateFor()
    {
        var store = new RecordingCoordinateStore();
        var path = Path.Combine(Path.GetTempPath(), "clone-a", "Acme.Orders.slnx");
        ITransactionalStore boxed = store;

        boxed.Open(path, EmptySourceDocumentReader.Instance);

        Assert.Equal(SolutionCoordinate.For(path), store.LastCoordinate);
        Assert.Equal(Path.GetFileName(path), store.LastCoordinate.SolutionFileName);
    }

    [Fact]
    [Trait("Requirement", "RP-05")]
    public void ITransactionalStoreAndIStoreSession_HaveNoProjectorMember()
    {
        Assert.DoesNotContain(
            typeof(ITransactionalStore).GetMembers(),
            member => LooksLikeProjectorMember(member.Name));
        Assert.DoesNotContain(
            typeof(IStoreSession).GetMembers(),
            member => LooksLikeProjectorMember(member.Name));
    }

    private static bool LooksLikeProjectorMember(string name) =>
        name.Contains("Projector", StringComparison.Ordinal)
        || name.Equals("Project", StringComparison.Ordinal);

    [Fact]
    [Trait("Requirement", "ENG-20")]
    [Trait("Requirement", "STOR-15")]
    public void IStoreSession_ExposesStageCommitAndAbortAsDistinctOperations()
    {
        var stage = typeof(IStoreSession).GetMethod("Stage");
        var commit = typeof(IStoreSession).GetMethod("Commit");
        var abort = typeof(IStoreSession).GetMethod("Abort");

        Assert.NotNull(stage);
        Assert.NotNull(commit);
        Assert.NotNull(abort);
        Assert.NotSame(stage, commit);
        Assert.NotSame(commit, abort);
        Assert.NotSame(stage, abort);

        var stageParameter = Assert.Single(stage.GetParameters());
        Assert.Equal(typeof(void), stage.ReturnType);
        Assert.Equal(typeof(FactualSnapshot), stageParameter.ParameterType);
        Assert.Equal("snapshot", stageParameter.Name);

        Assert.Equal(typeof(CommittedPublication), commit.ReturnType);
        Assert.Empty(commit.GetParameters());

        Assert.Equal(typeof(void), abort.ReturnType);
        Assert.Empty(abort.GetParameters());
    }

    [Fact]
    [Trait("Requirement", "ENG-20")]
    public void IStoreSession_DoesNotMergeStagingWithCommit()
    {
        var merged = typeof(IStoreSession)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(IStoreSession))
            .FirstOrDefault(method =>
                method.Name.Contains("Stage", StringComparison.Ordinal)
                && method.Name.Contains("Commit", StringComparison.Ordinal));

        Assert.True(
            merged is null,
            $"Staging and commit must be distinct operations, but '{merged?.Name}' combines them.");
    }

    private sealed class RecordingCoordinateStore : ITransactionalStore
    {
        public SolutionCoordinate LastCoordinate { get; private set; }

        public IStoreSession Open(SolutionCoordinate coordinate, ISourceDocumentReader sourceReader)
        {
            LastCoordinate = coordinate;
            return new IdleSession();
        }
    }

    private sealed class IdleSession : IStoreSession
    {
        public void Stage(FactualSnapshot snapshot)
        {
        }

        public CommittedPublication Commit() => new("idle", []);

        public void Abort()
        {
        }
    }
}
