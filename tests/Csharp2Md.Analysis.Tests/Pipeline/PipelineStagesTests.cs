using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineStagesTests
{
    [Fact]
    [Trait("Requirement", "ROSE-59")]
    [Trait("Requirement", "ROSE-31")]
    public void CreateDefault_IsTheRealStageSequence()
    {
        var stages = PipelineStages.CreateDefault();

        Assert.Collection(
            stages,
            stage => Assert.IsType<InventoryStage>(stage),
            stage => Assert.IsType<SemanticAnalysisStage>(stage),
            stage => Assert.IsType<ObservationExtractionStage>(stage),
            stage => Assert.IsType<ClassificationAndPromotionStage>(stage),
            stage => Assert.IsType<ValidationAndCoverageStage>(stage),
            stage => Assert.IsType<PersistenceStage>(stage));

        Assert.Equal(
            new[]
            {
                "Inventory",
                "Semantic Analysis",
                "Observation Extraction",
                "Classification and Promotion",
                "Validation and Coverage",
                "Persistence",
            },
            stages.Select(stage => stage.Name).ToArray());
    }

    [Fact]
    [Trait("Requirement", "EBC-27")]
    public void CreateDefault_RunsEveryClassifierPassInDependencyOrder()
    {
        var classification = Assert.IsType<ClassificationAndPromotionStage>(PipelineStages.CreateDefault()[3]);

        Assert.Equal(
            new[]
            {
                typeof(ComponentPass),
                typeof(EntryPointPass),
                typeof(BoundaryPass),
                typeof(ContractPass),
                typeof(PersistencePass),
                typeof(ConfigurationPass),
                typeof(RelationPass),
                typeof(InvokesPass),
                typeof(ExecutesPass),
            },
            classification.Passes.Select(pass => pass.GetType()).ToArray());
    }
}
