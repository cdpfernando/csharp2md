namespace Csharp2Md.Analysis.Pipeline;

internal sealed class PipelineOrchestrator
{
    private readonly ImmutableArray<IPipelineStage> _stages;

    public PipelineOrchestrator(ImmutableArray<IPipelineStage> stages)
    {
        if (stages.IsDefault || stages.Length != StubStages.DeclaredNames.Length)
        {
            throw new ArgumentException(
                $"The pipeline requires {StubStages.DeclaredNames.Length} stages in declared order.",
                nameof(stages));
        }

        for (var index = 0; index < stages.Length; index++)
        {
            var expected = StubStages.DeclaredNames[index];
            var actual = stages[index].Name;
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Stage {index + 1} must be named '{expected}', not '{actual}'.",
                    nameof(stages));
            }
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
                if (result.StructuralCorruption)
                {
                    return PipelineRunResult.Corrupted(hasUnknownsOrCandidatesOrFrontiers);
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
    public static PipelineRunResult Succeeded { get; } = new(PipelineCompletion.Succeeded, null);

    public static PipelineRunResult Cancelled { get; } = new(PipelineCompletion.Cancelled, null);

    public static PipelineRunResult Failed(string stageName) => new(PipelineCompletion.Failed, stageName);

    public static PipelineRunResult Completed(bool hasUnknownsOrCandidatesOrFrontiers) =>
        new(PipelineCompletion.Succeeded, null, false, hasUnknownsOrCandidatesOrFrontiers);

    public static PipelineRunResult Corrupted(bool hasUnknownsOrCandidatesOrFrontiers) =>
        new(PipelineCompletion.StructuralCorruption, null, true, hasUnknownsOrCandidatesOrFrontiers);
}

internal enum PipelineCompletion
{
    Succeeded,
    Cancelled,
    Failed,
    StructuralCorruption,
}
