using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineStagesTests
{
    [Fact]
    [Trait("Requirement", "ROSE-59")]
    public void ProductionConstructor_UsesPipelineStagesCreateDefault()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "AnalysisEngine.cs");
        Assert.True(File.Exists(path), $"AnalysisEngine source was not found at '{path}'.");

        var text = File.ReadAllText(path);
        Assert.Contains("PipelineStages.CreateDefault()", text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "this(store, StubStages.CreateDefault())",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-59")]
    public void CreateDefault_ReturnsDeclaredStageNames()
    {
        var names = PipelineStages.CreateDefault().Select(stage => stage.Name).ToArray();

        Assert.Equal(StubStages.DeclaredNames.ToArray(), names);
    }

    [Fact]
    [Trait("Requirement", "ROSE-31")]
    public void CreateDefault_UsesSemanticAnalysisStageNotStub()
    {
        var semantic = PipelineStages.CreateDefault()[1];

        Assert.IsType<SemanticAnalysisStage>(semantic);
        Assert.IsNotType<SemanticAnalysisStub>(semantic);
        Assert.IsType<InventoryStage>(PipelineStages.CreateDefault()[0]);
        Assert.IsType<ObservationExtractionStage>(PipelineStages.CreateDefault()[2]);
        Assert.IsNotType<ObservationExtractionStub>(PipelineStages.CreateDefault()[2]);
    }

    [Fact]
    [Trait("Requirement", "EBC-27")]
    public void CreateDefault_UsesClassificationAndPromotionStageNotStub()
    {
        var classification = PipelineStages.CreateDefault()[3];

        Assert.IsType<ClassificationAndPromotionStage>(classification);
        Assert.IsNotType<ClassificationAndPromotionStub>(classification);
        Assert.Equal("Classification and Promotion", classification.Name);
        Assert.Contains("new ComponentPass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
        Assert.Contains("new EntryPointPass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
    }

    private static string PipelineStagesPath() =>
        Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Pipeline",
            "PipelineStages.cs");
}
