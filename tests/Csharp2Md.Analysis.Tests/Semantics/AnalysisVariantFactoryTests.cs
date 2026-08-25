using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Identity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Analysis.Tests.Semantics;

public sealed class AnalysisVariantFactoryTests
{
    [Fact]
    [Trait("Requirement", "ROSE-25")]
    public async Task Create_FixtureProject_UsesLocalEnvironmentDebugConfigurationAndProjectSymbols()
    {
        var factory = new MsBuildWorkspaceFactory();
        await using var lease = await factory.Open(
            AcmeOrdersSolutionPath(),
            AnalysisVariantFactory.DefaultConfiguration,
            "net10.0",
            CancellationToken.None);
        var project = Assert.Single(
            lease.Solution.Projects,
            candidate => candidate.FilePath is not null
                && candidate.FilePath.Contains("Acme.Orders.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.IsType<CSharpParseOptions>(project.ParseOptions);
        var parseOptions = (CSharpParseOptions)project.ParseOptions;
        var shuffled = parseOptions.PreprocessorSymbolNames.Reverse();

        var variant = AnalysisVariantFactory.Create(
            project,
            "net10.0",
            AnalysisVariantFactory.DefaultConfiguration);

        Assert.Contains("configuration=Debug", variant.Value, StringComparison.Ordinal);
        Assert.Contains("environment=local", variant.Value, StringComparison.Ordinal);
        Assert.Contains("tfm=net10.0", variant.Value, StringComparison.Ordinal);
        Assert.Equal(
            AnalysisVariantId.Create("net10.0", "Debug", shuffled, "local").Value,
            variant.Value);
    }

    [Fact]
    [Trait("Requirement", "ROSE-24")]
    [Trait("Requirement", "ROSE-25")]
    public async Task ExecuteAsync_AcmeOrders_EmitsLocalDebugVariantsFromDeclaredTfm()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        await new InventoryStage().ExecuteAsync(context, CancellationToken.None);

        await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);

        Assert.Contains("net10.0", context.DeclaredTargetFrameworks, StringComparer.Ordinal);
        Assert.NotEmpty(context.AnalysisVariants);
        Assert.All(
            context.AnalysisVariants,
            variant =>
            {
                Assert.Contains("configuration=Debug", variant.Value, StringComparison.Ordinal);
                Assert.Contains("environment=local", variant.Value, StringComparison.Ordinal);
            });
        Assert.Contains(
            context.AnalysisVariants,
            variant => variant.Value.Contains("tfm=net10.0", StringComparison.Ordinal));
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
}
