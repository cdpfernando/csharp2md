using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class AlwaysWhenBindableWalkerTests
{
    [Fact]
    [Trait("Requirement", "ROSE-32")]
    [Trait("Requirement", "ROSE-33")]
    [Trait("Requirement", "ROSE-34")]
    [Trait("Requirement", "ROSE-35")]
    [Trait("Requirement", "ROSE-36")]
    [Trait("Requirement", "ROSE-47")]
    public async Task ExtractInto_AcmeOrders_YieldsEachAlwaysWhenBindableKindWithEmptyPayload()
    {
        var observations = await ExtractAcmeOrdersAsync();

        Assert.Contains(observations, observation => observation.Identity.Kind is ObservationKind.Invocation);
        Assert.Contains(observations, observation => observation.Identity.Kind is ObservationKind.ObjectCreation);
        Assert.Contains(observations, observation => observation.Identity.Kind is ObservationKind.TypeUsage);
        Assert.Contains(observations, observation => observation.Identity.Kind is ObservationKind.BaseType);
        Assert.Contains(observations, observation => observation.Identity.Kind is ObservationKind.AttributeUsage);
        Assert.All(
            observations.Where(observation => observation.Identity.Kind is ObservationKind.Invocation
                or ObservationKind.ObjectCreation
                or ObservationKind.TypeUsage
                or ObservationKind.BaseType
                or ObservationKind.AttributeUsage),
            observation => Assert.Empty(observation.Identity.Payload.Entries));
        Assert.All(
            observations,
            observation => Assert.All(
                observation.Identity.Payload.Entries,
                entry => Assert.IsType<StructuralLiteral>(entry.Value)));
    }

    [Fact]
    [Trait("Requirement", "ROSE-35")]
    public async Task ExtractInto_OrdersControllerBaseList_EmitsBaseTypeForControllerBase()
    {
        var observations = await ExtractAcmeOrdersAsync();

        Assert.Contains(
            observations,
            observation => observation.Identity.Kind is ObservationKind.BaseType
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal)
                && observation.Identity.Owner.Id.Value.Contains("OrdersController", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-32")]
    public async Task ExtractInto_BoundInvocation_IsOwnedByContainingCallable()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var invocations = observations
            .Where(observation =>
                observation.Identity.Kind is ObservationKind.Invocation
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(invocations);
        Assert.All(
            invocations,
            invocation => Assert.Contains("GetOrderStatus", invocation.Identity.Owner.Id.Value, StringComparison.Ordinal));
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
