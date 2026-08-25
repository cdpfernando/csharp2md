using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class DefaultPipelineZerosTests
{
    [Fact]
    [Trait("Requirement", "ROSE-13")]
    [Trait("Requirement", "ENG-15")]
    public async Task AnalyzeAsync_DefaultPipeline_InventoryFactsAreNonZeroAndLaterStagesStayZero()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var engine = new AnalysisEngine(new InMemoryTransactionalStore());
        var request = AnalysisRequest.Create([solutionPath]);

        var result = await engine.AnalyzeAsync(request, CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.False(result.HasUnpublishedSolution);
        Assert.True(outcome.HasUnknownsOrCandidatesOrFrontiers);
        Assert.Equal(8, outcome.Stages.Length);

        var inventory = outcome.Stages[0];
        Assert.Equal("Inventory", inventory.Name);
        Assert.True(inventory.FactCount > 0, $"Inventory fact count was {inventory.FactCount}.");
        Assert.Equal(0, inventory.ObservationCount);
        Assert.Equal(0, inventory.RelationCount);

        for (var index = 1; index < StubStages.DeclaredNames.Length; index++)
        {
            var report = outcome.Stages[index];
            Assert.Equal(StubStages.DeclaredNames[index], report.Name);
            Assert.Equal(0, report.FactCount);
            Assert.Equal(0, report.ObservationCount);
            Assert.Equal(0, report.RelationCount);
        }
    }
}
