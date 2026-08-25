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

    public async ValueTask<PipelineCompletion> RunAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        foreach (var stage in _stages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PipelineCompletion.Cancelled;
            }

            var result = await stage.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            context.Record(new StageReport(stage.Name, result.FactCount, result.ObservationCount, result.RelationCount));
        }

        return PipelineCompletion.Succeeded;
    }
}

internal enum PipelineCompletion
{
    Succeeded,
    Cancelled,
}
