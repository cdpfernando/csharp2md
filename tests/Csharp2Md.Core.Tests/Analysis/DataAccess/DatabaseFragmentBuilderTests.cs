using System.Security.Cryptography;
using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class DatabaseFragmentBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"csharp2md-dbfragment-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // DAD-13: a node reaches the fragment carrying its evidence, its kind and its resolution.
    [Fact]
    public void Build_ConfiguredTable_YieldsADatabaseObjectFactCarryingItsEvidence()
    {
        var fragment = Built(FullyMappedOrder());

        var node = Assert.Single(fragment.Facts.OfType<DatabaseObjectFact>());
        Assert.Equal("tb_order", node.Name);
        Assert.Equal(DatabaseObjectKind.Table, node.Kind);
        Assert.Equal(DatabaseObjectFactId.UnknownConnection, node.ConnectionName);
        Assert.Equal(FactKind.DatabaseObject, node.Header.Kind);
        Assert.Equal(FactResolution.Exact, node.Header.Resolution);
        Assert.Equal(
            "Data/OrderConfiguration.cs",
            Assert.Single(node.Header.Evidence).RelativePath);
    }

    // A column references its object, and the fragment validates only because that object is in it.
    [Fact]
    public void Build_ConfiguredColumn_ReferencesItsObjectAndValidatesCleanly()
    {
        var result = DatabaseFragmentBuilder.Build(FullyMappedOrder(), FactValidator.Validate);

        Assert.Empty(result.Diagnostics);
        var column = Assert.Single(result.Fragment!.Facts.OfType<DatabaseColumnFact>());
        Assert.Equal("order_status", column.Name);
        Assert.Equal(
            Assert.Single(result.Fragment.Facts.OfType<DatabaseObjectFact>()).ObjectId,
            column.ObjectId);
    }

    // Every persistence relation lands in the data partition with detector provenance and evidence,
    // which is what FactValidator's C2M-FV-005 rule demands of a runtime relation.
    [Fact]
    public void Build_Relations_LandInTheDataPartitionWithDetectorProvenanceAndEvidence()
    {
        var fragment = Built(FullyMappedOrder());

        var relations = fragment.Facts.OfType<RelationFact>().ToArray();
        Assert.NotEmpty(relations);
        Assert.All(relations, relation =>
        {
            Assert.Equal(RelationPartition.Data, relation.Partition);
            Assert.NotEmpty(relation.Header.Evidence);
            Assert.Contains(relation.Header.Provenance, provenance => provenance.DetectorId is not null);
        });
    }

    // DAD-14 survives the round trip into facts: an untargeted relation keeps its stated reason.
    [Fact]
    public void Build_ConventionMapping_KeepsItsUnresolvedReasonOnTheFact()
    {
        var fragment = Built(ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders")));

        var mapsTo = Assert.Single(
            fragment.Facts.OfType<RelationFact>(), relation => relation.RelationKind == "maps-to");
        Assert.Null(mapsTo.TargetId);
        Assert.Equal("convention-mapping", mapsTo.UnresolvedReason);
    }

    // Two identical observations in one member must not collapse onto one identity (C2M-FV-001).
    [Fact]
    public void Build_TwoIdenticalAccessesInOneMember_YieldTwoDistinctRelationIdentities()
    {
        var first = ResolverScenario.EntitySetAccess("Order", "Orders", DatabaseOperation.Read);
        var second = first with
        {
            Evidence = new Evidence(first.Evidence.DocumentId, first.Evidence.RelativePath, 14, 1, 14, 40),
        };

        var result = DatabaseFragmentBuilder.Build(
            ResolverScenario.Resolve(ResolverScenario.Symbols(ResolverScenario.Entity("Order")), first, second),
            FactValidator.Validate);

        Assert.Empty(result.Diagnostics);
        var reads = result.Fragment!.Facts.OfType<RelationFact>()
            .Where(relation => relation.RelationKind == "reads")
            .ToArray();
        Assert.Equal(2, reads.Length);
        Assert.Equal(2, reads.Select(relation => relation.RelationId.Value).Distinct(StringComparer.Ordinal).Count());
    }

    // Preserved SQL spans lines, and the identity grammar accepts only canonical single-line values.
    [Fact]
    public void Build_RelationCarryingMultilineSql_ProducesACanonicalIdentity()
    {
        var fragment = Built(ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            ResolverScenario.SqlAccess(null, null, DatabaseOperation.Update) with
            {
                ShapeConfidence = FactResolution.Unresolved,
                UnresolvedReason = "unreadable-sql-target",
                SqlText = "UPDATE (SELECT 1)\n  SET x = 1",
            }));

        var relation = Assert.Single(fragment.Facts.OfType<RelationFact>());

        Assert.DoesNotContain('\n', relation.RelationId.Value);
        Assert.DoesNotContain("  ", relation.RelationId.Value, StringComparison.Ordinal);
    }

    // A run over a codebase with no persistence code produces no fragment and no diagnostic.
    [Fact]
    public void Build_EmptyResolution_YieldsNoFragmentAndNoDiagnostics()
    {
        var result = DatabaseFragmentBuilder.Build(DatabaseResolution.Empty, FactValidator.Validate);

        Assert.Null(result.Fragment);
        Assert.Empty(result.Diagnostics);
    }

    // A validation failure surfaces exactly as a document fragment's does: no fragment, and the
    // validator's own diagnostics, with no new failure semantics layered on top.
    [Fact]
    public void Build_EvidenceOutsideTheDocumentExtent_YieldsNoFragmentAndTheValidatorsDiagnostic()
    {
        var result = DatabaseFragmentBuilder.Build(OutOfRangeEvidence(), FactValidator.Validate);

        Assert.Null(result.Fragment);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-FV-004", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    // The fragment must reach the manifest with a hash and length the aggregate writer can re-verify.
    [Fact]
    public void Build_PersistedFragment_ReportsTheHashAndLengthOfTheBytesOnDisk()
    {
        var fragment = Built(FullyMappedOrder());

        var stored = new FactStore(_root).Persist(fragment);

        var bytes = File.ReadAllBytes(Path.Combine(_root, "raw", stored.Reference.Value.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), stored.Sha256);
        Assert.Equal(bytes.Length, stored.ByteLength);
    }

    private static ValidatedFactFragment Built(DatabaseResolution resolution)
    {
        var result = DatabaseFragmentBuilder.Build(resolution, FactValidator.Validate);
        Assert.Empty(result.Diagnostics);
        return result.Fragment!;
    }

    /// <summary>An entity with a configured table, a configured column, and one read of its set.</summary>
    private static DatabaseResolution FullyMappedOrder() =>
        ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"),
            ResolverScenario.EntitySetAccess("Order", "Orders", DatabaseOperation.Read));

    /// <summary>A node evidenced past the end of the only document the fragment retained.</summary>
    private static DatabaseResolution OutOfRangeEvidence()
    {
        var documentId = DocumentFactId.Create(ResolverScenario.ProjectId, "Data/OrderConfiguration.cs");
        var objectId = DatabaseObjectFactId.Create(
            DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "tb_order");
        return new DatabaseResolution(
            [
                new ResolvedDatabaseObject(
                    objectId,
                    DatabaseObjectKind.Table,
                    "tb_order",
                    FactResolution.Exact,
                    [new Evidence(documentId, "Data/OrderConfiguration.cs", 99, 1, 99, 4)],
                    [DataAccessAnalyzerId.Create("csharp2md.dataaccess.efcore")]),
            ],
            [],
            [],
            [DocumentExtent.Create(documentId, "Data/OrderConfiguration.cs", [10])]);
    }
}
