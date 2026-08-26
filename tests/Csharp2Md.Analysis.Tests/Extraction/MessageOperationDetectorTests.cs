using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class MessageOperationDetectorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-40")]
    [Trait("Requirement", "EBC-17")]
    public async Task ExtractInto_PublishAsync_EmitsMessageOperationWithEventTypePayload()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var servicePath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "OrderService.cs");

        var publish = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.MessageOperation
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/OrderService.cs", StringComparison.Ordinal)
                && SpannedLines(servicePath, observation).Contains("PublishAsync", StringComparison.Ordinal));

        Assert.Equal(ObservationKind.MessageOperation, publish.Identity.Kind);
        Assert.Contains(
            publish.Identity.Payload.Entries,
            entry => entry.Key == "method-name" && entry.Value.Value == "PublishAsync");
        Assert.Contains(
            publish.Identity.Payload.Entries,
            entry => entry.Key == "type-argument" && entry.Value.Value.Contains("OrderPlaced", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-40")]
    [Trait("Requirement", "ROSE-42")]
    public async Task ExtractInto_BoundFindAndAdd_AreNotMessageOperation()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var controllerPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Api",
            "OrdersController.cs");
        var writesPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Data",
            "OrderDbContext.cs");

        Assert.DoesNotContain(
            observations,
            observation => observation.Identity.Kind is ObservationKind.MessageOperation
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal)
                && SpannedLines(controllerPath, observation).Contains("Find", StringComparison.Ordinal));
        Assert.DoesNotContain(
            observations,
            observation => observation.Identity.Kind is ObservationKind.MessageOperation
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderDbContext.cs", StringComparison.Ordinal)
                && SpannedLines(writesPath, observation).Contains(".Add(", StringComparison.Ordinal));
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
