using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class DataAccessDetectorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-41")]
    public async Task ExtractInto_SaveChanges_EmitsDataAccessWithEmptyPayload()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var writesPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Data",
            "OrderDbContext.cs");

        var saveChanges = observations
            .Where(observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderDbContext.cs", StringComparison.Ordinal)
                && SpannedLines(writesPath, observation).Contains("_context.SaveChanges", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(saveChanges);
        Assert.All(saveChanges, observation => Assert.Empty(observation.Identity.Payload.Entries));
    }

    [Fact]
    [Trait("Requirement", "ROSE-41")]
    [Trait("Requirement", "ROSE-42")]
    public async Task ExtractInto_OrderRepository_ProducesNoDataAccess()
    {
        var observations = await ExtractAcmeOrdersAsync();

        Assert.DoesNotContain(
            observations,
            observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("OrderRepository.cs", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-49")]
    public async Task ExtractInto_Fixture_YieldsEveryObservationKindAtLeastOnce()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var missing = Enum.GetValues<ObservationKind>()
            .Where(kind => observations.All(observation => observation.Identity.Kind != kind))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Fixture is missing observation kind(s): " + string.Join(", ", missing));
        Assert.All(
            Enum.GetValues<ObservationKind>(),
            kind => Assert.Contains(observations, observation => observation.Identity.Kind == kind));
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
