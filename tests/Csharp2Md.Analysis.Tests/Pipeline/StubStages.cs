using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

/// <summary>
/// A pipeline whose stages all do nothing, in the production sequence's order. Tests build on it to
/// exercise one real stage, or the orchestrator itself, without running the whole analysis.
/// </summary>
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
    ];

    internal static ImmutableArray<IPipelineStage> CreateDefault() =>
    [
        new NoOpStage("Inventory"),
        new NoOpStage("Semantic Analysis"),
        new NoOpStage("Observation Extraction"),
        new NoOpStage("Classification and Promotion"),
        new NoOpStage("Validation and Coverage"),
        new StagingNoOpStage(),
    ];
}

internal sealed class NoOpStage(string name) : IPipelineStage
{
    public string Name { get; } = name;

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(StageResult.Zero);
}

/// <summary>Stands in for the persistence stage: stages an empty snapshot so a run can still commit.</summary>
internal sealed class StagingNoOpStage : IPipelineStage
{
    public string Name => "Persistence";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        context.Session.Stage(FactualSnapshot.Empty);
        return ValueTask.FromResult(StageResult.Zero);
    }
}
