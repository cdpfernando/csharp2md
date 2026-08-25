using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class TransactionalStorePortTests
{
    [Fact]
    [Trait("Requirement", "ENG-20")]
    public void ITransactionalStore_Open_TakesSolutionKeyAndReturnsSession()
    {
        var open = typeof(ITransactionalStore).GetMethod(nameof(ITransactionalStore.Open));

        Assert.NotNull(open);
        Assert.Equal(typeof(IStoreSession), open.ReturnType);

        var parameter = Assert.Single(open.GetParameters());
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.Equal("solutionKey", parameter.Name);
    }

    [Fact]
    [Trait("Requirement", "ENG-20")]
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
        Assert.Equal(typeof(StagedFragment), stageParameter.ParameterType);
        Assert.Equal("fragment", stageParameter.Name);

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
}
