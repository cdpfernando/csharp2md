using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class ConfigurationDetectorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-38")]
    [Trait("Requirement", "ROSE-47")]
    public async Task ExtractInto_ConfigurationIndexer_EmitsConfigurationKeyOnly()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var controllerPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Api",
            "OrdersController.cs");

        var configuration = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.Configuration
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal)
                && SpannedLines(controllerPath, observation).Contains("Logging:Level", StringComparison.Ordinal));

        var entry = Assert.Single(configuration.Identity.Payload.Entries);
        Assert.IsType<StructuralLiteral>(entry.Value);
        Assert.Equal(LiteralRole.ConfigurationKey, entry.Value.Role);
        Assert.Equal("Logging:Level", entry.Value.Value);
        Assert.All(
            configuration.Identity.Payload.Entries,
            payload => Assert.Equal(LiteralRole.ConfigurationKey, payload.Value.Role));
        Assert.DoesNotContain(
            configuration.Identity.Payload.Entries,
            payload => payload.Value.Role is not LiteralRole.ConfigurationKey);
        Assert.DoesNotContain(
            configuration.Identity.Payload.Entries,
            payload => !string.Equals(payload.Value.Value, "Logging:Level", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-38")]
    [Trait("Requirement", "ROSE-42")]
    public async Task ExtractInto_NonConfigurationIndexer_DoesNotEmitConfiguration()
    {
        var observations = await ExtractAcmeOrdersAsync();

        Assert.DoesNotContain(
            observations,
            observation => observation.Identity.Kind is ObservationKind.Configuration
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("OrderRepository.cs", StringComparison.Ordinal));
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
