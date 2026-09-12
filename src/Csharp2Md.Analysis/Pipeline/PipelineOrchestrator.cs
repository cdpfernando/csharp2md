namespace Csharp2Md.Analysis.Pipeline;

internal sealed class PipelineOrchestrator
{
    private readonly ImmutableArray<IPipelineStage> _stages;

    public PipelineOrchestrator(ImmutableArray<IPipelineStage> stages)
    {
        if (stages.IsDefaultOrEmpty)
        {
            throw new ArgumentException("The pipeline requires at least one stage.", nameof(stages));
        }

        _stages = stages;
    }

    public async ValueTask<PipelineRunResult> RunAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var hasUnknownsOrCandidatesOrFrontiers = false;
        foreach (var stage in _stages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PipelineRunResult.Cancelled;
            }

            try
            {
                var result = await stage.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                context.Record(new StageReport(stage.Name, result.FactCount, result.ObservationCount, result.RelationCount));
                hasUnknownsOrCandidatesOrFrontiers |= result.HasUnknownsOrCandidatesOrFrontiers;
                if (result.StructuralCorruption || context.Accumulator.StructuralCorruption)
                {
                    context.Detail ??= context.Accumulator.CollidingIdentity;
                    return PipelineRunResult.Corrupted(hasUnknownsOrCandidatesOrFrontiers);
                }

                if (result.AbortPublication)
                {
                    return PipelineRunResult.Aborted(hasUnknownsOrCandidatesOrFrontiers);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return PipelineRunResult.Failed(stage.Name);
            }
        }

        return PipelineRunResult.Completed(hasUnknownsOrCandidatesOrFrontiers);
    }
}

internal readonly record struct PipelineRunResult(
    PipelineCompletion Status,
    string? FailedStageName,
    bool StructuralCorruption = false,
    bool HasUnknownsOrCandidatesOrFrontiers = false)
{
    public static PipelineRunResult Cancelled { get; } = new(PipelineCompletion.Cancelled, null);

    public static PipelineRunResult Failed(string stageName) => new(PipelineCompletion.Failed, stageName);

    public static PipelineRunResult Completed(bool hasUnknownsOrCandidatesOrFrontiers) =>
        new(PipelineCompletion.Succeeded, null, false, hasUnknownsOrCandidatesOrFrontiers);

    public static PipelineRunResult Corrupted(bool hasUnknownsOrCandidatesOrFrontiers) =>
        new(PipelineCompletion.StructuralCorruption, null, true, hasUnknownsOrCandidatesOrFrontiers);

    public static PipelineRunResult Aborted(bool hasUnknownsOrCandidatesOrFrontiers) =>
        new(PipelineCompletion.Aborted, null, false, hasUnknownsOrCandidatesOrFrontiers);
}

internal enum PipelineCompletion
{
    Succeeded,
    Cancelled,
    Failed,
    StructuralCorruption,
    Aborted,
}
