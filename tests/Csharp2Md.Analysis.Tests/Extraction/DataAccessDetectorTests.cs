using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class DataAccessDetectorTests
{
    [Fact]
    [Trait("Requirement", "ROSE-41")]
    [Trait("Requirement", "PK-01")]
    [Trait("Requirement", "PK-03")]
    public async Task ExtractInto_SaveChanges_EmitsDataAccessWithUnknownOperationAndContextType()
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
        Assert.All(
            saveChanges,
            observation =>
            {
                var entries = observation.Identity.Payload.Entries;
                Assert.Equal(2, entries.Length);
                Assert.Contains(
                    entries,
                    entry => entry.Key == "operation"
                        && entry.Value.Role == LiteralRole.ProtocolName
                        && entry.Value.Value == "unknown");
                Assert.Contains(
                    entries,
                    entry => entry.Key == "context-type"
                        && entry.Value.Role == LiteralRole.ProtocolName
                        && entry.Value.Value == "global::Acme.Orders.Data.OrderDbContext");
            });
    }

    [Fact]
    [Trait("Requirement", "PK-01")]
    [Trait("Requirement", "PK-02")]
    public async Task ExtractInto_PlaceOrderAdd_EmitsInsertOperationWithEntityType()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var insertAccess = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderDbContext.cs", StringComparison.Ordinal)
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "operation" && entry.Value.Value == "insert"));

        var entries = insertAccess.Identity.Payload.Entries;
        Assert.Contains(
            entries,
            entry => entry.Key == "entity-type" && entry.Value.Value == "global::Acme.Orders.Data.Order");
    }

    [Fact]
    [Trait("Requirement", "PK-05")]
    public async Task ExtractInto_WhereOverDbSet_EmitsSingleFilteredFieldName()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var whereAccess = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderDbContext.cs", StringComparison.Ordinal)
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "field-names" && entry.Value.Value == "Id"));

        var entries = whereAccess.Identity.Payload.Entries;
        Assert.Contains(entries, entry => entry.Key == "operation" && entry.Value.Value == "read");
        Assert.Contains(
            entries,
            entry => entry.Key == "entity-type" && entry.Value.Value == "global::Acme.Orders.Data.Order");
    }

    [Fact]
    [Trait("Requirement", "PK-05")]
    public async Task ExtractInto_SelectProjectionOverDbSet_EmitsOrdinalSortedJoinedFieldNames()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var selectAccess = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderDbContext.cs", StringComparison.Ordinal)
                && observation.Identity.Payload.Entries.Any(
                    entry => entry.Key == "field-names" && entry.Value.Value == "Amount|Id|Status"));

        Assert.Contains(
            selectAccess.Identity.Payload.Entries,
            entry => entry.Key == "operation" && entry.Value.Value == "read");
    }

    [Fact]
    [Trait("Requirement", "PK-01")]
    public async Task ExtractInto_EveryDataAccessObservation_CarriesAnOperationEntry()
    {
        var observations = await ExtractAcmeOrdersAsync();
        var dataAccesses = observations.Where(observation => observation.Identity.Kind is ObservationKind.DataAccess);

        Assert.NotEmpty(dataAccesses);
        Assert.All(
            dataAccesses,
            observation => Assert.Contains(observation.Identity.Payload.Entries, entry => entry.Key == "operation"));
    }

    [Fact]
    [Trait("Requirement", "PK-04")]
    public async Task ExtractInto_ConstantSelectStatement_CarriesParsedSqlEvidenceAndNoStatementText()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var sqlAccess = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderSqlQueries.cs", StringComparison.Ordinal)
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "sql-target" && entry.Value.Value == "Orders")
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "operation" && entry.Value.Value == "read"));

        var entries = sqlAccess.Identity.Payload.Entries;
        Assert.Contains(entries, entry => entry.Key == "sql-operation" && entry.Value.Value == "read");
        Assert.Contains(
            entries,
            entry => entry.Key == "sql-columns" && entry.Value.Value == "Id|Status");
        Assert.DoesNotContain(
            entries,
            entry => entry.Value.Value.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Requirement", "PK-04")]
    public async Task ExtractInto_ConstantDeleteWithBracketedTarget_CarriesUnquotedSqlTarget()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var deleteAccess = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderSqlQueries.cs", StringComparison.Ordinal)
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "operation" && entry.Value.Value == "delete"));

        var entries = deleteAccess.Identity.Payload.Entries;
        Assert.Contains(entries, entry => entry.Key == "sql-operation" && entry.Value.Value == "delete");
        Assert.Contains(entries, entry => entry.Key == "sql-target" && entry.Value.Value == "Orders");
    }

    [Fact]
    [Trait("Requirement", "PK-04")]
    public async Task ExtractInto_ConstantExecStatement_CarriesExecuteOperationAndProcedureTarget()
    {
        var observations = await ExtractAcmeOrdersAsync();

        var execAccess = Assert.Single(
            observations,
            observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderSqlQueries.cs", StringComparison.Ordinal)
                && observation.Identity.Payload.Entries.Any(entry => entry.Key == "sql-target" && entry.Value.Value == "usp_RebuildOrderTotals"));

        Assert.Contains(
            execAccess.Identity.Payload.Entries,
            entry => entry.Key == "operation" && entry.Value.Value == "execute");
    }

    [Fact]
    [Trait("Requirement", "PK-04")]
    public async Task ExtractInto_NonConstantInterpolatedStatement_EmitsUnknownOperationAndNoSqlEntries()
    {
        var observations = await ExtractAcmeOrdersAsync();

        // SelectAllFrom(tableName) is the file's only entity-type=Order access whose statement is
        // genuinely non-constant (an actual interpolation hole); the SELECT/DELETE/EXEC accesses on
        // the same DbSet all carry a constant statement and therefore sql-* entries, so this check
        // holds for every remaining entity-type=Order occurrence in the file, including the raw
        // member access to `_context.Orders` that never carries sql-* evidence either way.
        var entityAccesses = observations
            .Where(observation => observation.Identity.Kind is ObservationKind.DataAccess
                && observation.Locator.RelativePath.Replace('\\', '/')
                    .EndsWith("Acme.Orders/Data/OrderSqlQueries.cs", StringComparison.Ordinal)
                && observation.Identity.Payload.Entries.Any(
                    entry => entry.Key == "entity-type" && entry.Value.Value == "global::Acme.Orders.Data.Order")
                && !observation.Identity.Payload.Entries.Any(entry => entry.Key == "sql-operation"))
            .ToArray();

        Assert.NotEmpty(entityAccesses);
        Assert.All(
            entityAccesses,
            observation =>
            {
                var entries = observation.Identity.Payload.Entries;
                Assert.Contains(entries, entry => entry.Key == "operation" && entry.Value.Value == "unknown");
                Assert.DoesNotContain(entries, entry => entry.Key is "sql-operation" or "sql-target" or "sql-columns");
            });
    }

    [Fact]
    [Trait("Requirement", "PK-09")]
    public async Task ExtractInto_Fixture_NoPayloadEntryContainsEitherFixturePassword()
    {
        var observations = await ExtractAcmeOrdersAsync();

        Assert.All(
            observations,
            observation => Assert.All(
                observation.Identity.Payload.Entries,
                entry =>
                {
                    Assert.DoesNotContain("inline-fixture-secret", entry.Value.Value, StringComparison.Ordinal);
                    Assert.DoesNotContain("appsettings-fixture-secret", entry.Value.Value, StringComparison.Ordinal);
                }));
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
