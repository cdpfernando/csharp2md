using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class EfMappingPayloadTests
{
    [Fact]
    [Trait("Requirement", "PK-07")]
    public async Task ExtractInto_ToTableInvocation_EmitsTableNameAndEntityTypeFromEnclosingEntityCall()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var toTable = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.Invocation
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "table-name"));

        var entries = toTable.Identity.Payload.Entries;
        Assert.Contains(
            entries,
            entry => entry.Key == "table-name"
                && entry.Value.Role == LiteralRole.TableName
                && entry.Value.Value == "order_headers");
        Assert.Contains(
            entries,
            entry => entry.Key == "entity-type"
                && entry.Value.Role == LiteralRole.ProtocolName
                && entry.Value.Value == "global::Acme.Orders.Data.Order");
    }

    [Fact]
    [Trait("Requirement", "PK-07")]
    public async Task ExtractInto_HasColumnNameInvocation_EmitsFieldNameEntityTypeAndPropertyName()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var hasColumnName = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.Invocation
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "field-name"));

        var entries = hasColumnName.Identity.Payload.Entries;
        Assert.Contains(
            entries,
            entry => entry.Key == "field-name"
                && entry.Value.Role == LiteralRole.FieldName
                && entry.Value.Value == "order_status");
        Assert.Contains(
            entries,
            entry => entry.Key == "entity-type" && entry.Value.Value == "global::Acme.Orders.Data.Order");
        Assert.Contains(
            entries,
            entry => entry.Key == "property-name"
                && entry.Value.Role == LiteralRole.FieldName
                && entry.Value.Value == "Status");
    }

    [Fact]
    [Trait("Requirement", "PK-07")]
    public async Task ExtractInto_EntityInvocation_EmitsEntityType()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var entityCalls = observations
            .Where(observation => observation.Identity.Kind is ObservationKind.Invocation
                && observation.Identity.Payload.Entries.Any(
                    entry => entry.Key == "entity-type" && entry.Value.Value == "global::Acme.Orders.Data.Order")
                && observation.Identity.Payload.Entries.All(entry => entry.Key is "method-name" or "target-type" or "entity-type"))
            .ToArray();

        Assert.NotEmpty(entityCalls);
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
