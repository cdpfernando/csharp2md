using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

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
            observations.Where(observation => observation.Identity.Kind is ObservationKind.ObjectCreation
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

    [Fact]
    [Trait("Requirement", "ROSE-45")]
    public async Task ExtractInto_BoundInvocation_UsesSemanticAndBoundDiagnostic()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var invocations = observations
            .Where(observation => observation.Identity.Kind is ObservationKind.Invocation)
            .ToArray();

        Assert.Contains(
            invocations,
            invocation => invocation.ExtractionMethod is EvidenceMethod.Semantic
                && string.Equals(invocation.Diagnostic.Code, "bound", StringComparison.Ordinal));
        Assert.All(
            invocations.Where(invocation => string.Equals(invocation.Diagnostic.Code, "bound", StringComparison.Ordinal)),
            invocation =>
            {
                Assert.Equal(EvidenceMethod.Semantic, invocation.ExtractionMethod);
                Assert.Equal("bound", invocation.Diagnostic.Code);
            });
    }

    [Fact]
    [Trait("Requirement", "ROSE-45")]
    public async Task ExtractInto_UnboundInvocation_EmitsKindWithFailureDiagnosticAndNoTargetFactId()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-unbound-");
        try
        {
            var solutionPath = WriteUnboundInvocationSolution(tree.FullName);
            var context = new PipelineContext(new SwallowingSession(), solutionPath);
            try
            {
                await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
                await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
                AlwaysWhenBindableWalker.ExtractInto(context, CancellationToken.None);

                var unbound = Assert.Single(
                    context.Accumulator.ToSnapshot().Observations,
                    observation => observation.Identity.Kind is ObservationKind.Invocation
                        && !string.Equals(observation.Diagnostic.Code, "bound", StringComparison.Ordinal));

                Assert.Equal(EvidenceMethod.Syntactic, unbound.ExtractionMethod);
                Assert.Equal("unbound", unbound.Diagnostic.Code);
                Assert.NotEqual("bound", unbound.Diagnostic.Code);
                Assert.Contains(
                    unbound.Identity.Payload.Entries,
                    entry => entry.Key == "method-name" && entry.Value.Value == "MissingTarget");
                Assert.DoesNotContain(
                    unbound.Identity.Payload.Entries,
                    entry => entry.Value.Value.Contains("id1:", StringComparison.Ordinal));
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

    private static string WriteUnboundInvocationSolution(string root)
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
                public void Run() => MissingTarget();
            }
            """);

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
