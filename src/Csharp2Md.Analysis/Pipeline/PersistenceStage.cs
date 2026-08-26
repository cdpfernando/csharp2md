using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Pipeline;

internal sealed class PersistenceStage : IPipelineStage
{
    public string Name => "Persistence";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;
        context.Session.Stage(context.Accumulator.ToSnapshot());
        return StubStages.ZeroResult();
    }
}
