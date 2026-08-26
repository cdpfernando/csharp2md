using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class AssignmentDetectorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-37")]
    public async Task ExtractInto_PayOrderStatusWrite_EmitsAssignmentWithEmptyPayload()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var orderDbContext = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Data",
            "OrderDbContext.cs");

        var statusWrite = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.Assignment
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderDbContext.cs", StringComparison.Ordinal)
                && SpannedLines(orderDbContext, observation).Contains("order.Status", StringComparison.Ordinal));

        Assert.Equal(ObservationKind.Assignment, statusWrite.Identity.Kind);
        Assert.Empty(statusWrite.Identity.Payload.Entries);
    }

    [Fact]
    [Trait("Requirement", "ROSE-42")]
    public async Task ExtractInto_NonEntityPropertyWrite_DoesNotEmitAssignment()
    {
        var observations = await ExtractAcmeOrdersAsync();

        Assert.DoesNotContain(
            observations,
            observation => observation.Identity.Kind is ObservationKind.Assignment
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("ReceiverShapes.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(
            observations,
            observation => observation.Identity.Kind is ObservationKind.Assignment
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
