using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class RouteDeclarationDetectorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-39")]
    public async Task ExtractInto_HttpGetAttribute_EmitsRouteDeclarationWithRouteLiteral()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var controllerPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Api",
            "OrdersController.cs");

        var route = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.RouteDeclaration
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal)
                && SpannedLines(controllerPath, observation).Contains("HttpGet(\"orders/{id}\")", StringComparison.Ordinal));

        var entry = Assert.Single(route.Identity.Payload.Entries);
        Assert.Equal(LiteralRole.Route, entry.Value.Role);
        Assert.Equal("orders/{id}", entry.Value.Value);
        Assert.Contains(
            observations,
            observation => observation.Identity.Kind is ObservationKind.AttributeUsage
                && observation.Locator.RelativePath == route.Locator.RelativePath
                && observation.Locator.Span == route.Locator.Span);
    }

    private static string SpannedLines(string absolutePath, Observation observation)
    {
        var lines = File.ReadAllLines(absolutePath);
        var span = observation.Locator.Span;
        return string.Join(
            Environment.NewLine,
            lines[(span.StartLine - 1)..span.EndLine]);
    }

    private static async Task<Observation[]> ExtractAcmeOrdersAsync()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            AlwaysWhenBindableWalker.ExtractInto(context, CancellationToken.None);
            return [.. context.Accumulator.ToSnapshot().Observations];
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }
}
