using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineCancellationTests
{
    [Fact]
    [Trait("Requirement", "ENG-18")]
    public async Task AnalyzeAsync_CancelAfterStageThree_RecordsThreeStagesAndDoesNotCommit()
    {
        using var cts = new CancellationTokenSource();
        var executed = new List<string>();
        var stages = StubStages.DeclaredNames
            .Select((name, index) => (IPipelineStage)new RecordingStage(
                name,
                executed,
                onExecute: index switch
                {
                    0 => static context => context.Session.Stage(
                        new StagedFragment(ArtifactRole.Payload, "early", [1])),
                    2 => _ => cts.Cancel(),
                    _ => null,
                }))
            .ToImmutableArray();
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store, stages);
        var solutionPath = "alpha.sln";

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), cts.Token);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.True(result.HasUnpublishedSolution);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            executed);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            outcome.Stages.Select(report => report.Name).ToArray());
        Assert.Equal(3, outcome.Stages.Length);
        Assert.False(store.TryGetPublication(Path.GetFullPath(solutionPath), out _));
    }
}
