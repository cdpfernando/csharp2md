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
        Assert.Contains("new BoundaryPass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
        Assert.Contains("new ContractPass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
        Assert.Contains("new PersistencePass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
        Assert.Contains("new RelationPass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
        Assert.Contains("new InvokesPass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
        Assert.Contains("new ExecutesPass()", File.ReadAllText(PipelineStagesPath()), StringComparison.Ordinal);
        var source = File.ReadAllText(PipelineStagesPath());
        var passConstructors = new[]
        {
            "new ComponentPass()",
            "new EntryPointPass()",
            "new BoundaryPass()",
            "new ContractPass()",
            "new PersistencePass()",
            "new RelationPass()",
            "new InvokesPass()",
            "new ExecutesPass()",
        };
        Assert.Equal(8, passConstructors.Count(ctor => source.Contains(ctor, StringComparison.Ordinal)));
        Assert.True(
            source.IndexOf("new ComponentPass()", StringComparison.Ordinal)
                < source.IndexOf("new EntryPointPass()", StringComparison.Ordinal)
                && source.IndexOf("new EntryPointPass()", StringComparison.Ordinal)
                    < source.IndexOf("new BoundaryPass()", StringComparison.Ordinal)
                && source.IndexOf("new BoundaryPass()", StringComparison.Ordinal)
                    < source.IndexOf("new ContractPass()", StringComparison.Ordinal)
                && source.IndexOf("new ContractPass()", StringComparison.Ordinal)
                    < source.IndexOf("new PersistencePass()", StringComparison.Ordinal)
                && source.IndexOf("new PersistencePass()", StringComparison.Ordinal)
                    < source.IndexOf("new RelationPass()", StringComparison.Ordinal)
                && source.IndexOf("new RelationPass()", StringComparison.Ordinal)
                    < source.IndexOf("new InvokesPass()", StringComparison.Ordinal)
                && source.IndexOf("new InvokesPass()", StringComparison.Ordinal)
                    < source.IndexOf("new ExecutesPass()", StringComparison.Ordinal),
            "CreateDefault pass order must be ComponentPass → EntryPointPass → BoundaryPass → ContractPass → PersistencePass → RelationPass → InvokesPass → ExecutesPass.");
    }

    private static string PipelineStagesPath() =>
        Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Pipeline",
            "PipelineStages.cs");
}
