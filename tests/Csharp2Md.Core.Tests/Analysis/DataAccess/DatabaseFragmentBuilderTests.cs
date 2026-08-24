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

    // RELR-32: DatabaseFragmentBuilder no longer mints relation facts at all - resolution.Relations
    // leaves DatabaseMappingResolver.Resolve as RawRelation claims for the RelationClaimAccumulator
    // instead (AD-018: RelationResolver is the only writer of RelationFact). A resolution carrying only
    // relations and no objects/columns therefore now has nothing for this builder to build.
    [Fact]
    public void Build_ResolutionWithOnlyRelationsAndNoObjectsOrColumns_YieldsNoFragment()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders"));
        Assert.Empty(resolution.Objects);
        Assert.Empty(resolution.Columns);
        Assert.NotEmpty(resolution.Relations);

        var result = DatabaseFragmentBuilder.Build(resolution, FactValidator.Validate);

        Assert.Null(result.Fragment);
        Assert.Empty(result.Diagnostics);
    }

    // The relation-carrying test coverage this file used to hold (data-partition/evidence assignment,
    // duplicate-observation survival, multi-line SQL text preservation, an untargeted relation's stated
    // reason) now lives in DatabaseMappingResolverTests.cs against DatabaseMappingResolver.Resolve's own
    // RawRelation output, since that is where those properties are actually set (RELR-32); building a
    // fragment out of them is no longer part of the round trip this file exercises.

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
