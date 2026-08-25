using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Pipeline;

internal static class StubStages
{
    internal static readonly ImmutableArray<string> DeclaredNames =
    [
        "Inventory",
        "Semantic Analysis",
        "Observation Extraction",
        "Classification and Promotion",
        "Validation and Coverage",
        "Persistence",
        "Retrieval Projection",
        "Batch Composition",
    ];

    internal static ImmutableArray<IPipelineStage> CreateDefault() =>
    [
        new InventoryStub(),
        new SemanticAnalysisStub(),
        new ObservationExtractionStub(),
        new ClassificationAndPromotionStub(),
        new ValidationAndCoverageStub(),
        new PersistenceStub(),
        new RetrievalProjectionStub(),
        new BatchCompositionStub(),
    ];

    internal static ValueTask<StageResult> ZeroResult() =>
        ValueTask.FromResult(new StageResult(0, 0, 0, StructuralCorruption: false, HasUnknownsOrCandidatesOrFrontiers: false));
}

internal sealed class InventoryStub : IPipelineStage
{
    public string Name => "Inventory";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        StubStages.ZeroResult();
}

internal sealed class SemanticAnalysisStub : IPipelineStage
{
    public string Name => "Semantic Analysis";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        StubStages.ZeroResult();
}

internal sealed class ObservationExtractionStub : IPipelineStage
{
    public string Name => "Observation Extraction";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        StubStages.ZeroResult();
}

internal sealed class ClassificationAndPromotionStub : IPipelineStage
{
    public string Name => "Classification and Promotion";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        StubStages.ZeroResult();
}

internal sealed class ValidationAndCoverageStub : IPipelineStage
{
    public string Name => "Validation and Coverage";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        StubStages.ZeroResult();
}

internal sealed class PersistenceStub : IPipelineStage
{
    public string Name => "Persistence";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        context.Session.Stage(FactualSnapshot.Empty);
        return StubStages.ZeroResult();
    }
}

internal sealed class RetrievalProjectionStub : IPipelineStage
{
    public string Name => "Retrieval Projection";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        StubStages.ZeroResult();
}

internal sealed class BatchCompositionStub : IPipelineStage
{
    public string Name => "Batch Composition";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        StubStages.ZeroResult();
}
