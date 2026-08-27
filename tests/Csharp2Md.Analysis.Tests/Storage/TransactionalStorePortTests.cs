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

        var parameters = open.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("solutionKey", parameters[0].Name);
        Assert.Equal(typeof(ISourceDocumentReader), parameters[1].ParameterType);
        Assert.Equal("sourceReader", parameters[1].Name);
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
}
