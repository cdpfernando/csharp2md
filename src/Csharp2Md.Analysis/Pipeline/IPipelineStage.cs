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
    bool AbortPublication = false)
{
    /// <summary>A stage that produced nothing and found no corruption.</summary>
    public static StageResult Zero { get; } = new(0, 0, 0, StructuralCorruption: false, HasUnknownsOrCandidatesOrFrontiers: false);
}
