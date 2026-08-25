using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineStageFailureTests
{
    [Fact]
    [Trait("Requirement", "ENG-19")]
    [Trait("Requirement", "ENG-25")]
    public async Task AnalyzeAsync_ThrowingSubstitute_SkipsLaterStagesAndDoesNotPublish()
    {
        var executed = new List<string>();
        var thrower = new ThrowingStage("Classification and Promotion");
        var stages = StubStages.DeclaredNames
            .Select((name, index) => index == 3
                ? (IPipelineStage)thrower
                : new RecordingStage(
                    name,
                    executed,
                    onExecute: index == 0
                        ? static context => context.Session.Stage(FactualSnapshot.Empty)
                        : null))
            .ToImmutableArray();
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store, stages);
        var solutionPath = "alpha.sln";

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.Equal(thrower.Name, outcome.FailingStage);
        Assert.True(result.HasUnpublishedSolution);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            executed);
        Assert.DoesNotContain("Validation and Coverage", executed);
        Assert.DoesNotContain("Persistence", executed);
        Assert.DoesNotContain("Retrieval Projection", executed);
        Assert.DoesNotContain("Batch Composition", executed);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            outcome.Stages.Select(report => report.Name).ToArray());
        Assert.False(store.TryGetPublication(Path.GetFullPath(solutionPath), out _));
    }
}

internal sealed class ThrowingStage : IPipelineStage
{
    public ThrowingStage(string name) => Name = name;

    public string Name { get; }

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        throw new InvalidOperationException($"Stage '{Name}' failed.");
    }
}
