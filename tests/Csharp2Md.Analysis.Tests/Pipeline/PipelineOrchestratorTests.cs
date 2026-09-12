using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineOrchestratorTests
{
    [Fact]
    [Trait("Requirement", "ENG-13")]
    public async Task RunAsync_ExecutesRecordingProbesInDeclaredOrder()
    {
        var executed = new List<string>();
        var probes = StubStages.DeclaredNames
            .Select(name => (IPipelineStage)new RecordingStage(name, executed))
            .ToImmutableArray();
        var context = new PipelineContext(new SwallowingSession(), "unused.sln");

        await new PipelineOrchestrator(probes).RunAsync(context, CancellationToken.None);

        Assert.Equal(StubStages.DeclaredNames.ToArray(), executed);
        Assert.Equal(StubStages.DeclaredNames.ToArray(), context.Reports.Select(report => report.Name).ToArray());
    }

    [Fact]
    [Trait("Requirement", "ENG-13")]
    public async Task RunAsync_ExecutesTheStagesItWasGiven_InTheOrderItWasGivenThem()
    {
        var executed = new List<string>();
        ImmutableArray<IPipelineStage> reversed =
        [
            .. StubStages.DeclaredNames.Reverse().Select(name => (IPipelineStage)new RecordingStage(name, executed)),
        ];
        var context = new PipelineContext(new SwallowingSession(), "unused.sln");

        await new PipelineOrchestrator(reversed).RunAsync(context, CancellationToken.None);

        Assert.Equal(StubStages.DeclaredNames.Reverse().ToArray(), executed);
    }

    [Fact]
    [Trait("Requirement", "ENG-13")]
    public void Construction_RejectsAnEmptyPipeline()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new PipelineOrchestrator(ImmutableArray<IPipelineStage>.Empty));

        Assert.Equal("stages", exception.ParamName);
    }

}

internal sealed class RecordingStage : IPipelineStage
{
    private readonly List<string> _executed;
    private readonly Action<PipelineContext>? _onExecute;

    public RecordingStage(string name, List<string> executed, Action<PipelineContext>? onExecute = null)
    {
        Name = name;
        _executed = executed;
        _onExecute = onExecute;
    }

    public string Name { get; }

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        _executed.Add(Name);
        _onExecute?.Invoke(context);
        return ValueTask.FromResult(StageResult.Zero);
    }
}

internal sealed class ResultStage : IPipelineStage
{
    private readonly StageResult _result;
    private readonly List<string>? _executed;
    private readonly Action<PipelineContext>? _onExecute;

    public ResultStage(
        string name,
        StageResult result,
        List<string>? executed = null,
        Action<PipelineContext>? onExecute = null)
    {
        Name = name;
        _result = result;
        _executed = executed;
        _onExecute = onExecute;
    }

    public string Name { get; }

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _executed?.Add(Name);
        _onExecute?.Invoke(context);
        return ValueTask.FromResult(_result);
    }
}

internal sealed class SwallowingSession : IStoreSession
{
    public void Stage(FactualSnapshot snapshot)
    {
    }

    public CommittedPublication Commit() => new("unused", []);

    public void Abort()
    {
    }
}
