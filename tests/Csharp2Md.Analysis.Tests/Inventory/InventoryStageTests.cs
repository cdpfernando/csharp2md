using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;

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

    [Fact]
    [Trait("Requirement", "ROSE-13")]
    public async Task ExecuteAsync_AcmeOrders_EmitsExactlyOneSolutionFactAndNonZeroFactCount()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        var stage = new InventoryStage();

        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.FactCount > 0);
        Assert.False(result.AbortPublication);
        Assert.Single(context.Accumulator.ToSnapshot().Facts.OfType<Solution>());
        Assert.Contains(
            context.Accumulator.ToSnapshot().Facts.OfType<Document>(),
            document => string.Equals(document.RelativePath, "Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-13")]
    public async Task ExecuteAsync_AcmeOrders_RecordsDeclaredTargetFrameworkFromCsproj()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);

        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

        Assert.Contains("net10.0", context.DeclaredTargetFrameworks, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-13")]
    public async Task ExecuteAsync_TargetFrameworksElement_RecordsEachDeclaredTfm()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-inv-tfms-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(
                Path.Combine(projectDir, "App.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net10.0;net9.0</TargetFrameworks>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), "class Program;");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");
            var context = new PipelineContext(new SwallowingSession(), solutionPath);

            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

            Assert.Contains("net10.0", context.DeclaredTargetFrameworks, StringComparer.Ordinal);
            Assert.Contains("net9.0", context.DeclaredTargetFrameworks, StringComparer.Ordinal);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
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
