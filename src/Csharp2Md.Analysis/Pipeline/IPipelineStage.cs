namespace Csharp2Md.Analysis.Pipeline;

internal interface IPipelineStage
{
    string Name { get; }

    ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken);
}

internal readonly record struct StageResult(
    int FactCount,
    int ObservationCount,
    int RelationCount,
    bool StructuralCorruption,
    bool HasUnknownsOrCandidatesOrFrontiers,
    bool AbortPublication = false);
