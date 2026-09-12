using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ObservationExtractionStageTests
{
    [Fact]
    [Trait("Requirement", "ROSE-48")]
    [Trait("Requirement", "ROSE-59")]
    public async Task AnalyzeAsync_DefaultEngine_ObservationExtractionCountIsGreaterThanZero()
    {
        var engine = new AnalysisEngine(new InMemoryTransactionalStore());

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create([AcmeOrdersSolutionPath()]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        var extraction = outcome.Stages[2];
        Assert.Equal("Observation Extraction", extraction.Name);
        Assert.True(extraction.ObservationCount > 0, $"Observation Extraction observation count was {extraction.ObservationCount}.");
    }

    [Fact]
    [Trait("Requirement", "ROSE-48")]
    public async Task ExecuteAsync_PlantedJsonDocument_ProducesNoObservationsForThatPath()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-json-doc-");
        try
        {
            var solutionPath = WriteSolutionWithJsonDocument(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
                await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);

                Assert.DoesNotContain(
                    context.Accumulator.ToSnapshot().Observations,
                    observation => observation.Locator.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(
                    context.CSharpDocuments,
                    document => document.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(
                    context.CSharpDocuments,
                    document => document.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                context.BoundSolution?.Dispose();
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-48")]
    public async Task ExecuteAsync_AcmeOrders_DisposesBoundSolutionAtEndOfStage()
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            Assert.NotNull(context.BoundSolution);
            Assert.False(context.BoundSolution.IsDisposed);

            await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);

            Assert.NotNull(context.BoundSolution);
            Assert.True(context.BoundSolution.IsDisposed);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-48")]
    public void ObservationExtractionStage_IsInternalAndCreateDefaultWiresIt()
    {
        var stage = PipelineStages.CreateDefault()[2];

        Assert.IsType<ObservationExtractionStage>(stage);
        Assert.False(typeof(ObservationExtractionStage).IsPublic);
        Assert.False(typeof(ObservationExtractionStage).IsNestedPublic);
        Assert.DoesNotContain(
            typeof(ObservationExtractionStage).GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)),
            type => type.Namespace is not null && type.Namespace.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));
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

    private static string WriteSolutionWithJsonDocument(string root)
    {
        var projectDir = Path.Combine(root, "App");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDir, "Host.cs"),
            """
            class Host
            {
                public void Run() => System.Console.WriteLine();
            }
            """);
        File.WriteAllText(Path.Combine(projectDir, "data.json"), """{ "key": "value" }""");

        var solutionPath = Path.Combine(root, "App.slnx");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="App/App.csproj" />
            </Solution>
            """);
        return solutionPath;
    }
}
