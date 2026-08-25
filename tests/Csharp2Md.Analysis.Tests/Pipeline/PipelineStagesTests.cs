using Csharp2Md.Analysis.Pipeline;

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
}
