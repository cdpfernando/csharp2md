using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class DefaultPipelineZerosTests
{
    [Fact]
    [Trait("Requirement", "ROSE-13")]
    [Trait("Requirement", "ROSE-59")]
    [Trait("Requirement", "ROSE-60")]
    [Trait("Requirement", "EBC-35")]
    [Trait("Requirement", "ENG-15")]
    public async Task AnalyzeAsync_DefaultPipeline_ProducesFactsObservationsAndRelationsInTheRightStages()
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
        Assert.Equal(6, outcome.Stages.Length);

        var inventory = outcome.Stages[0];
        Assert.Equal("Inventory", inventory.Name);
        Assert.True(inventory.FactCount > 0, $"Inventory fact count was {inventory.FactCount}.");
        Assert.Equal(0, inventory.ObservationCount);
        Assert.Equal(0, inventory.RelationCount);

        var semantic = outcome.Stages[1];
        Assert.Equal("Semantic Analysis", semantic.Name);
        Assert.True(semantic.FactCount > 0, $"Semantic Analysis fact count was {semantic.FactCount}.");
        Assert.Equal(0, semantic.ObservationCount);
        Assert.Equal(0, semantic.RelationCount);

        var extraction = outcome.Stages[2];
        Assert.Equal("Observation Extraction", extraction.Name);
        Assert.Equal(0, extraction.FactCount);
        Assert.True(extraction.ObservationCount > 0, $"Observation Extraction observation count was {extraction.ObservationCount}.");
        Assert.True(extraction.RelationCount > 0, $"Observation Extraction relation count was {extraction.RelationCount}.");

        var persistence = outcome.Stages[5];
        Assert.Equal("Persistence", persistence.Name);

        AssertZeroProduction(outcome.Stages[4], "Validation and Coverage");

        var classification = outcome.Stages[3];
        Assert.Equal("Classification and Promotion", classification.Name);
        Assert.True(classification.FactCount > 0, $"Classification fact count was {classification.FactCount}.");
        Assert.Equal(0, classification.ObservationCount);
        Assert.True(classification.RelationCount > 0, $"Classification relation count was {classification.RelationCount}.");
    }

    private static void AssertZeroProduction(StageReport report, string name)
    {
        Assert.Equal(name, report.Name);
        Assert.Equal(0, report.FactCount);
        Assert.Equal(0, report.ObservationCount);
        Assert.Equal(0, report.RelationCount);
    }
}
