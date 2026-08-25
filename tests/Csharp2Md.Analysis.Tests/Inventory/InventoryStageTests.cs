using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class InventoryStageTests
{
    [Fact]
    [Trait("Requirement", "ROSE-11")]
    public async Task ExecuteAsync_AcmeDoesNotExist_RecordsMissingProjectAndSucceeds()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        var stage = new InventoryStage();

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.AbortPublication);
        Assert.False(result.StructuralCorruption);
        var missing = Assert.Single(
            context.Accumulator.ToSnapshot().Diagnostics,
            record => string.Equals(record.Code, "missing-project", StringComparison.Ordinal));
        Assert.Equal("Acme.DoesNotExist/Acme.DoesNotExist.csproj", missing.IdentityOrKey);
        Assert.False(Path.IsPathRooted(missing.IdentityOrKey));
        Assert.Contains("Acme.DoesNotExist", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-11")]
    public async Task ExecuteAsync_MissingListedProject_SetsUnknownsAndDoesNotAbort()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        var stage = new InventoryStage();

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.HasUnknownsOrCandidatesOrFrontiers);
        Assert.False(result.AbortPublication);
        Assert.False(result.StructuralCorruption);
        Assert.Equal("Inventory", stage.Name);
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private sealed class SwallowingSession : IStoreSession
    {
        public void Stage(FactualSnapshot snapshot)
        {
        }

        public CommittedPublication Commit() => new("unused", []);

        public void Abort()
        {
        }
    }
}
