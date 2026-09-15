using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class PersistenceIntegrationTests
{
    [Fact]
    [Trait("Requirement", "PK-20")]
    [Trait("Requirement", "PK-38")]
    [Trait("Requirement", "PK-42")]
    [Trait("Requirement", "PK-51")]
    public async Task AnalyzeAsync_AcmeOrders_PublishesTheSpecifiedPersistencePackage()
    {
        var (outcome, publication) = await AnalyzeAcmeOrdersAsync();
        Assert.Equal(PublicationStatus.Committed, outcome.Status);

        var persistence = ReadShard<PersistenceFactsShard>(publication, "facts/persistence.json");
        var store = Assert.Single(persistence.DataStores);
        Assert.Equal("OrdersDb", store.Name.Value);
        Assert.Equal("relational", store.Technology);

        var headers = Assert.Single(
            persistence.DataObjects,
            dataObject => dataObject.TableName.Value == "order_headers");
        Assert.Equal("explicit-confirmation", headers.MappingState);
        Assert.Equal("table", headers.Form);
        Assert.Equal(store.Identity.Id, headers.Store.Id);

        var orders = Assert.Single(
            persistence.DataObjects,
            dataObject => dataObject.TableName.Value == "Orders");
        Assert.Equal("conventional-candidate", orders.MappingState);
        Assert.NotEqual(headers.Identity.Id, orders.Identity.Id);

        var orderLines = Assert.Single(
            persistence.DataObjects,
            dataObject => dataObject.TableName.Value == "OrderLines");
        Assert.Equal("conventional-candidate", orderLines.MappingState);

        var status = Assert.Single(
            persistence.DataFields,
            field => field.FieldName.Value == "order_status");
        Assert.Equal("explicit-confirmation", status.MappingState);
        Assert.Equal(headers.Identity.Id, status.DataObject.Id);

        var idField = Assert.Single(
            persistence.DataFields.Where(field => field.FieldName.Value == "Id"
                && field.DataObject.Id == headers.Identity.Id));
        Assert.Equal("conventional-candidate", idField.MappingState);
        var amountField = Assert.Single(
            persistence.DataFields.Where(field => field.FieldName.Value == "Amount"
                && field.DataObject.Id == headers.Identity.Id));
        Assert.Equal("conventional-candidate", amountField.MappingState);

        var mapsTo = ReadOptionalRelations(publication, "relations/confirmed/maps-to.json");
        Assert.Contains(
            mapsTo,
            relation => relation.Kind == "maps-to"
                && relation.Target.Id == headers.Identity.Id
                && relation.Facets.Any(facet => facet.AxisName == "mapping-role" && facet.WireValue == "data-object-mapping")
                && relation.EvidenceMethod == "Configured");
        Assert.Contains(
            mapsTo,
            relation => relation.Kind == "maps-to"
                && relation.Target.Id == status.Identity.Id
                && relation.Facets.Any(facet => facet.AxisName == "mapping-role" && facet.WireValue == "data-field-mapping")
                && relation.EvidenceMethod == "Configured");

        var candidates = ReadOptionalArray<CandidateLinkDto>(publication, "relations/candidates.json");
        Assert.Contains(
            candidates,
            link => link.Kind == "maps-to" && link.ProposedTarget.Id == orderLines.Identity.Id);
        Assert.Contains(
            candidates,
            link => link.Kind == "maps-to" && link.ProposedTarget.Id == idField.Identity.Id);
        Assert.Contains(
            candidates,
            link => link.Kind == "maps-to" && link.ProposedTarget.Id == amountField.Identity.Id);
        Assert.DoesNotContain(mapsTo, relation => relation.Target.Id == orderLines.Identity.Id);
        Assert.DoesNotContain(mapsTo, relation => relation.Target.Id == idField.Identity.Id);
        Assert.DoesNotContain(mapsTo, relation => relation.Target.Id == amountField.Identity.Id);

        var updates = persistence.DataOperations
            .Where(operation => operation.Operation == "update" && operation.Target.Id == headers.Identity.Id)
            .ToArray();
        var update = Assert.Single(updates);
        var accessesData = ReadOptionalRelations(publication, "relations/confirmed/accesses-data.json");
        var updateAccesses = accessesData
            .Where(relation => relation.Kind == "accesses-data" && relation.Target.Id == update.Identity.Id)
            .ToArray();
        Assert.Equal(2, updateAccesses.Length);
        Assert.Contains(updateAccesses, relation => relation.Source.Id.Contains("PayOrder", StringComparison.Ordinal));
        Assert.Contains(updateAccesses, relation => relation.Source.Id.Contains("Reprice", StringComparison.Ordinal));

        var unresolved = ReadOptionalArray<UnresolvedRecordDto>(publication, "relations/unresolved.json");
        Assert.Contains(
            unresolved,
            record => record.Source.Id.Contains("SelectAllFrom", StringComparison.Ordinal)
                && record.Kind == "operates-on"
                && record.Cause == "InsufficientEvidence");
        Assert.DoesNotContain(
            persistence.DataObjects,
            dataObject => dataObject.TableName.Value.Contains('{', StringComparison.Ordinal));

        Assert.DoesNotContain(
            persistence.DataStores.Select(dto => dto.Identity.Id)
                .Concat(persistence.DataObjects.Select(dto => dto.Identity.Id))
                .Concat(persistence.DataFields.Select(dto => dto.Identity.Id))
                .Concat(persistence.DataOperations.Select(dto => dto.Identity.Id))
                .Concat(accessesData.Select(dto => dto.Source.Id + dto.Target.Id))
                .Concat(candidates.Select(dto => dto.Source.Id + dto.ProposedTarget.Id))
                .Concat(unresolved.Select(dto => dto.Source.Id)),
            id => id.Contains("OrderRepository", StringComparison.Ordinal));

        var diagnostics = CanonicalJson.Read<DiagnosticsEnvelope>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "diagnostics.json").Payload.AsSpan());
        var coverage = Assert.Single(diagnostics.Records, record => record.Code == "persistence-coverage");
        Assert.Contains("Recognized data-access occurrences", coverage.Message, StringComparison.Ordinal);
        Assert.Contains("resolved to operation and target", coverage.Message, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b\d+(\.\d+)?\s*%", coverage.Message);
        Assert.DoesNotContain("percent", coverage.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"(?i)\b(pass|fail|passed|failed|verdict)\b", coverage.Message);
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAcmeOrdersAsync()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey) =>
        ShardedFactsReader.Read<T>(publication.ArtifactsInPublicationOrder, canonicalKey);

    private static ImmutableArray<ConfirmedRelationDto> ReadOptionalRelations(
        CommittedPublication publication,
        string canonicalKey) =>
        ReadOptionalArray<ConfirmedRelationDto>(publication, canonicalKey);

    /// <summary>
    /// T52 made the derived ~32 KiB ceiling the live default, so a flat record-array family may now
    /// legitimately be sharded into "&lt;stem&gt;.&lt;bucket&gt;.json" artifacts instead of staying one
    /// file at its base key -- this merges every shard back into one array, matching what
    /// <c>FactualPackageReader.ReadShardedArray</c> does for a real reader.
    /// </summary>
    private static ImmutableArray<T> ReadOptionalArray<T>(CommittedPublication publication, string canonicalKey)
    {
        var stem = canonicalKey.EndsWith(".json", StringComparison.Ordinal)
            ? canonicalKey[..^".json".Length]
            : canonicalKey;
        var shardKeys = publication.ArtifactsInPublicationOrder
            .Select(static artifact => artifact.CanonicalKey)
            .Where(key => key == canonicalKey
                || (key.StartsWith(stem + ".", StringComparison.Ordinal) && key.EndsWith(".json", StringComparison.Ordinal)))
            .OrderBy(static key => key, StringComparer.Ordinal);

        var records = ImmutableArray.CreateBuilder<T>();
        foreach (var key in shardKeys)
        {
            var fragment = publication.ArtifactsInPublicationOrder.Single(artifact => artifact.CanonicalKey == key);
            records.AddRange(CanonicalJson.Read<ImmutableArray<T>>(fragment.Payload.AsSpan()));
        }

        return records.ToImmutable();
    }
}
