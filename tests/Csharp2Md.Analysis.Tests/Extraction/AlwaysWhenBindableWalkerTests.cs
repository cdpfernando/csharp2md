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
    public async Task Invocation_BoundToMethod_DiagnosticMessageContainsSignature()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-bound-sig-");
        try
        {
            var observations = await ExtractTempSolutionAsync(
                tree.FullName,
                """
                class Host
                {
                    public void Run() => Target();
                    public void Target() {}
                }
                """);

            var invocation = Assert.Single(
                observations,
                observation => observation.Identity.Kind is ObservationKind.Invocation
                    && string.Equals(observation.Diagnostic.Code, "bound", StringComparison.Ordinal));

            Assert.StartsWith("bound::", invocation.Diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("metadata=Target", invocation.Diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal("bound", invocation.Diagnostic.Code);
            Assert.DoesNotContain(
                invocation.Identity.Payload.Entries,
                entry => entry.Key.Contains("signature", StringComparison.Ordinal));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-45")]
    public async Task Invocation_BoundToNonMethod_DiagnosticMessageIsPlainBound()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-bound-nonmethod-");
        try
        {
            var observations = await ExtractTempSolutionAsync(
                tree.FullName,
                """
                class Host
                {
                    public void Run(Host other) => Target();
                    public void Target() {}
                }
                """);

            var typeUsages = observations
                .Where(observation => observation.Identity.Kind is ObservationKind.TypeUsage)
                .ToArray();

            Assert.NotEmpty(typeUsages);
            Assert.All(
                typeUsages,
                usage =>
                {
                    Assert.Equal("bound", usage.Diagnostic.Code);
                    Assert.Equal("bound", usage.Diagnostic.Message);
                });
            Assert.DoesNotContain(
                observations,
                observation => observation.Identity.Kind is ObservationKind.Invocation
                    && string.Equals(observation.Diagnostic.Message, "bound", StringComparison.Ordinal));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-45")]
    public async Task Invocation_Unbound_DiagnosticMessageIsUnbound()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-unbound-message-");
        try
        {
            var observations = await ExtractTempSolutionAsync(
                tree.FullName,
                """
                class Host
                {
                    public void Run() => MissingTarget();
                }
                """);

            var unbound = Assert.Single(
                observations,
                observation => observation.Identity.Kind is ObservationKind.Invocation);

            Assert.Equal("unbound", unbound.Diagnostic.Code);
            Assert.False(
                unbound.Diagnostic.Message.StartsWith("bound::", StringComparison.Ordinal),
                unbound.Diagnostic.Message);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-45")]
    public void TryExtractTargetSignature_BoundSignatureMessage_ReturnsSignature()
    {
        var signature = "sig1:kind=method|container=global::Host|metadata=Target|arity=0|type=void";

        Assert.Equal(signature, AlwaysWhenBindableWalker.TryExtractTargetSignature("bound::" + signature));
    }

    [Fact]
    [Trait("Requirement", "ROSE-45")]
    public void TryExtractTargetSignature_PlainBound_ReturnsNull()
    {
        Assert.Null(AlwaysWhenBindableWalker.TryExtractTargetSignature("bound"));
        Assert.Null(AlwaysWhenBindableWalker.TryExtractTargetSignature("unbound"));
        Assert.Null(AlwaysWhenBindableWalker.TryExtractTargetSignature("bound::"));
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

    private static async Task<Observation[]> ExtractTempSolutionAsync(string root, string hostSource)
    {
        var solutionPath = WriteTempHostSolution(root, hostSource);
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

    private static string WriteUnboundInvocationSolution(string root) =>
        WriteTempHostSolution(
            root,
            """
            class Host
            {
                public void Run() => MissingTarget();
            }
            """);

    private static string WriteTempHostSolution(string root, string hostSource)
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
        File.WriteAllText(Path.Combine(projectDir, "Host.cs"), hostSource);

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
