using Csharp2Md.Analysis.Pipeline;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class ObservationExtractionStage : IPipelineStage
{
    public string Name => "Observation Extraction";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            if (context.BoundSolution is null)
            {
                return ValueTask.FromResult(new StageResult(0, 0, 0, StructuralCorruption: false, HasUnknownsOrCandidatesOrFrontiers: false));
            }

            AlwaysWhenBindableWalker.ExtractInto(context, cancellationToken);
            var relationCount = ContainsRelationEmitter.Emit(context, cancellationToken);
            ProjectMetadataEmitter.Emit(context, cancellationToken);
            ConfigurationDocumentReader.Emit(context, cancellationToken);
            var snapshot = context.Accumulator.ToSnapshot();
            return ValueTask.FromResult(new StageResult(
                0,
                snapshot.Observations.Length,
                relationCount,
                StructuralCorruption: false,
                HasUnknownsOrCandidatesOrFrontiers: false));
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }
}
