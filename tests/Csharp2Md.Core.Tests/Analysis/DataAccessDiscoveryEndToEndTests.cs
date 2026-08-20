using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// T35: spec.md's P1 EF Core Independent Test, restated against a real default-mode
/// <see cref="AnalysisEngine.AnalyzeAsync"/> run over <c>fixtures/SyntheticSolution</c> - the same
/// fixture-and-run structure <c>RelationCollectorEndToEndTests</c> uses. Every assertion here maps to a
/// <c>DAD-NN</c> acceptance criterion, so the criteria are proven against the emitted files rather than
/// against a hand-built resolver input.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DataAccessDiscoveryEndToEndTests(DataAccessDiscoveryFixture fixture)
    : IClassFixture<DataAccessDiscoveryFixture>
{
    // The Independent Test names a default (syntax-only, untrusted) run; a trusted run would prove a
    // different mode's behaviour.
    [Fact]
    public void IndependentTest_TheRunUnderTest_IsTheDefaultSyntaxOnlyUntrustedMode()
    {
        Assert.Equal("syntax-only", fixture.Manifest.Analysis.Requested);
        Assert.Equal("syntax-only", fixture.Manifest.Analysis.Effective);
        Assert.Equal("untrusted", fixture.Manifest.Trust);
    }

    // DAD-01: one exposes per DbSet property, sourced at the declaring context type, naming the entity.
    [Fact]
    public void DAD01_EachDbSetProperty_ExposesItsEntityFromTheDeclaringContextType()
    {
        var exposes = fixture.DataRelations.Where(relation => relation.RelationKind == "exposes").ToArray();

        Assert.Equal(2, exposes.Length);
        Assert.Equal(
            ["Order", "OrderLine"],
            exposes.Select(relation => Detail(relation, "target_text")).Order(StringComparer.Ordinal));
        Assert.All(exposes, relation => Assert.Contains("OrderDbContext", relation.SourceId, StringComparison.Ordinal));
        Assert.All(exposes, relation => Assert.Null(relation.TargetId));
    }

    // DAD-02: no ToTable anywhere in the run leaves OrderLine mapped by convention to its DbSet name.
    [Fact]
    public void DAD02_UnconfiguredEntity_MapsToItsDbSetNameAtHeuristicWithNoTarget()
    {
        var convention = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "maps-to" && Detail(relation, "mapping") == "convention");

        Assert.Equal("heuristic", convention.Header.Resolution);
        Assert.Null(convention.TargetId);
        Assert.Equal("OrderLines", Detail(convention, "target_text"));
        Assert.Contains("OrderLine", convention.SourceId, StringComparison.Ordinal);
    }

    // DAD-03: the ToTable literal is what mints the node, and the entity's mapping points at it.
    [Fact]
    public void DAD03_ConfiguredTableLiteral_MintsAnExactTableNodeThatTheEntityMapsTo()
    {
        var table = Assert.Single(fixture.Database.Objects, node => node.Name == "order_headers");
        Assert.Equal("table", table.Kind);
        Assert.Equal("exact", table.Header.Resolution);

        var configured = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "maps-to" && Detail(relation, "mapping") == "configured");
        Assert.Equal(table.ObjectId, configured.TargetId);
        Assert.Equal("exact", configured.Header.Resolution);
        Assert.Equal("order_headers", Detail(configured, "target_text"));
        Assert.Contains("Order", configured.SourceId, StringComparison.Ordinal);
    }

    // DAD-04: Order is both exposed by a DbSet and configured, and only the configured mapping survives.
    [Fact]
    public void DAD04_EntityWithBothAConventionAndAConfiguredMapping_EmitsOnlyTheConfiguredOne()
    {
        var configured = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "maps-to" && Detail(relation, "mapping") == "configured");
        Assert.EndsWith("class%20Order", configured.SourceId, StringComparison.Ordinal);

        var mappingsForOrder = fixture.DataRelations
            .Where(relation => relation.RelationKind == "maps-to" && relation.SourceId == configured.SourceId)
            .ToArray();

        var only = Assert.Single(mappingsForOrder);
        Assert.Equal("configured", Detail(only, "mapping"));
    }

    // DAD-05: the HasColumnName literal mints a column owned by the entity's object, and the property
    // maps onto it exactly.
    [Fact]
    public void DAD05_ConfiguredColumnLiteral_MintsAColumnOwnedByTheEntitysObject()
    {
        var table = Assert.Single(fixture.Database.Objects, node => node.Name == "order_headers");
        var column = Assert.Single(fixture.Database.Columns, node => node.Name == "order_status");
        Assert.Equal(table.ObjectId, column.ObjectId);
        Assert.Equal("exact", column.Header.Resolution);

        var mapping = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "maps-property-to-column"
                && Detail(relation, "mapping") == "configured");
        Assert.Equal(column.ColumnId, mapping.TargetId);
        Assert.Equal("exact", mapping.Header.Resolution);
        Assert.Equal("order_status", Detail(mapping, "target_text"));
        Assert.Contains("Status", mapping.SourceId, StringComparison.Ordinal);
    }

    // DAD-06: a property with no HasColumnName names itself, targets nothing, and stays heuristic.
    [Fact]
    public void DAD06_UnconfiguredProperty_MapsToItsOwnNameAtHeuristicWithNoTarget()
    {
        var conventions = fixture.DataRelations
            .Where(relation => relation.RelationKind == "maps-property-to-column"
                && Detail(relation, "mapping") == "convention")
            .ToArray();

        Assert.All(conventions, relation => Assert.Equal("heuristic", relation.Header.Resolution));
        Assert.All(conventions, relation => Assert.Null(relation.TargetId));
        Assert.Contains(conventions, relation => Detail(relation, "target_text") == "Amount");
        Assert.Contains(conventions, relation => Detail(relation, "target_text") == "Sku");
        Assert.DoesNotContain(conventions, relation => Detail(relation, "target_text") == "order_status");
    }

    // DAD-07 and brief 7: the read targets the entity's mapped object and says what it did.
    [Fact]
    public void DAD07_ReadingADbSet_EmitsAReadOperationTargetingTheEntitysMappedObject()
    {
        var table = Assert.Single(fixture.Database.Objects, node => node.Name == "order_headers");

        var read = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "reads" && SourcedAt(relation, "GetOrder"));

        Assert.Equal("read", Detail(read, "operation"));
        Assert.Equal("Orders", Detail(read, "target_text"));
        Assert.Equal(table.ObjectId, read.TargetId);
        Assert.Equal("exact", read.Header.Resolution);
    }

    // DAD-08 and brief 7: "which columns does GetOrder read?" answers Id, Status and Amount.
    [Fact]
    public void DAD08_LinqProjectionOutsideWhere_EmitsOneReadColumnPerReferencedProperty()
    {
        var readColumns = fixture.DataRelations
            .Where(relation => relation.RelationKind == "reads-column" && SourcedAt(relation, "GetOrder"))
            .ToArray();

        Assert.Equal(
            ["Amount", "Id", "Status"],
            readColumns.Select(relation => Detail(relation, "target_text")).Order(StringComparer.Ordinal));
        Assert.All(readColumns, relation => Assert.Equal("read", Detail(relation, "usage")));
    }

    // DAD-09 and brief 7: "which column does it filter by?" answers Id, and only Id.
    [Fact]
    public void DAD09_WhereLambda_EmitsOneFiltersByPerReferencedProperty()
    {
        var filter = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "filters-by" && SourcedAt(relation, "GetOrder"));

        Assert.Equal("Id", Detail(filter, "target_text"));
        Assert.Equal("filter", Detail(filter, "usage"));
    }

    // DAD-10: Add proves an insert, and the relation kind carries the coarse direction.
    [Fact]
    public void DAD10_AddOnADbSet_EmitsAWriteWhoseOperationIsInsert()
    {
        var write = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "writes" && SourcedAt(relation, "PlaceOrder"));

        Assert.Equal("insert", Detail(write, "operation"));
        Assert.Equal("Orders", Detail(write, "target_text"));
        Assert.NotNull(write.TargetId);
    }

    // DAD-11 and brief 8: PayOrder appears as a writer of the column Order.Status maps to.
    [Fact]
    public void DAD11_TrackedWriteMatchingOneExposedEntity_TargetsThatPropertysColumnAtHeuristic()
    {
        var column = Assert.Single(fixture.Database.Columns, node => node.Name == "order_status");

        var write = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "writes-column" && SourcedAt(relation, "PayOrder"));

        Assert.Equal(column.ColumnId, write.TargetId);
        Assert.Equal("heuristic", write.Header.Resolution);
        Assert.Equal("write", Detail(write, "usage"));
        Assert.Equal("order.Status", Detail(write, "target_text"));
    }

    // DAD-12: Amount is declared by both exposed entities, so the write stays a candidate.
    [Fact]
    public void DAD12_TrackedWriteMatchingSeveralExposedEntities_StaysACandidateWithNoTarget()
    {
        var write = Assert.Single(
            fixture.DataRelations,
            relation => relation.RelationKind == "writes-column" && SourcedAt(relation, "Reprice"));

        Assert.Null(write.TargetId);
        Assert.Equal("candidate", write.Header.Resolution);
        Assert.Equal("ambiguous-entity-attribution", write.UnresolvedReason);
        Assert.Equal("order.Amount", Detail(write, "target_text"));
        Assert.Equal("write", Detail(write, "usage"));
    }

    // DAD-13: every persistence relation and node carries a located span, not just a document id.
    [Fact]
    public void DAD13_EveryPersistenceRelationAndNode_CarriesLocatedEvidence()
    {
        var evidence = fixture.DataRelations.Select(relation => relation.Header)
            .Concat(fixture.Database.Objects.Select(node => node.Header))
            .Concat(fixture.Database.Columns.Select(node => node.Header))
            .SelectMany(header => header.Evidence)
            .ToArray();

        Assert.NotEmpty(evidence);
        Assert.All(evidence, span =>
        {
            Assert.NotEmpty(span.DocumentId);
            Assert.NotEmpty(span.RelativePath);
            Assert.True(span.StartLine >= 1 && span.StartColumn >= 1, "one-based start position");
            Assert.True(span.EndLine >= span.StartLine, "end line at or after start line");
        });
    }

    // DAD-17: OrderRepository is a repository in name only. Nothing about the name is evidence.
    [Fact]
    public void DAD17_NameOnlyRepositoryDocument_ProducesNoPersistenceFactsAtAll()
    {
        const string Document = "Data/OrderRepository.cs";

        Assert.Contains(fixture.AnalysedDocuments, path => path.EndsWith(Document, StringComparison.Ordinal));
        Assert.DoesNotContain(
            fixture.DataRelations,
            relation => relation.Header.Evidence.Any(span =>
                span.RelativePath.EndsWith(Document, StringComparison.Ordinal)));
        Assert.DoesNotContain(fixture.Database.Objects, node => node.Name.Contains("Repository", StringComparison.Ordinal));
    }

    // DAD-19: three statements name the same object, and the catalogue holds one entry for all of them.
    [Fact]
    public void DAD19_ObjectNamedBySeveralStatements_HasOneCatalogueEntryWithMergedEvidence()
    {
        var repeated = Assert.Single(fixture.Database.Objects, node => node.Name == "Orders");

        Assert.Equal(3, repeated.Header.Evidence.Length);
        Assert.Equal(
            repeated.Header.Evidence.Length,
            repeated.Header.Evidence.Distinct().Count());
        Assert.Equal(
            fixture.Database.Objects.Select(node => node.ObjectId).Distinct(StringComparer.Ordinal).Count(),
            fixture.Database.Objects.Length);
    }

    private static bool SourcedAt(RelationFactJson relation, string memberName) =>
        relation.SourceId.Contains(memberName, StringComparison.Ordinal);

    private static string? Detail(RelationFactJson relation, string key) =>
        relation.Details?.FirstOrDefault(detail => detail.Key == key)?.Value;
}

/// <summary>
/// One default-mode analysis run over the three restorable fixture services, plus typed access to the
/// two files the persistence stage writes.
/// </summary>
public sealed class DataAccessDiscoveryFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-dad-e2e-{Guid.NewGuid():N}");

    public ImmutableArray<RelationFactJson> DataRelations { get; private set; }

    internal DatabaseAggregate Database { get; private set; } = null!;

    internal FactualManifest Manifest { get; private set; } = null!;

    public ImmutableArray<string> AnalysedDocuments { get; private set; }

    public async Task InitializeAsync()
    {
        await RunAsync(Output);

        var raw = TopicLayout.RawRoot(Output);
        Manifest = Deserialize(
            Path.Combine(raw, "facts", "manifest.json"), AggregateJsonContext.Default.FactualManifest);
        DataRelations = Deserialize(
            Path.Combine(raw, "facts", "relations", "data.json"),
            AggregateJsonContext.Default.RelationAggregate).Entries;
        Database = Deserialize(
            Path.Combine(raw, "facts", "database.json"), AggregateJsonContext.Default.DatabaseAggregate);
        AnalysedDocuments = AnalysedDocumentPaths(raw);
    }

    /// <summary>
    /// Every document the run actually persisted a fragment for, read the way
    /// <c>RelationCollectorEndToEndFixture</c> reads them. Without this, "no facts for that document"
    /// could mean the document was never analysed.
    /// </summary>
    private static ImmutableArray<string> AnalysedDocumentPaths(string raw)
    {
        var paths = ImmutableArray.CreateBuilder<string>();
        foreach (var path in Directory.EnumerateFiles(
            Path.Combine(raw, "facts", "document"), "*.json", SearchOption.AllDirectories))
        {
            var fragment = FactualJsonSerializer.Deserialize(File.ReadAllBytes(path));
            paths.AddRange(fragment.Documents.Select(static document => document.RelativePath));
        }

        return paths.ToImmutable();
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(Output))
        {
            Directory.Delete(Output, recursive: true);
        }

        return Task.CompletedTask;
    }

    internal static async Task RunAsync(string output)
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-dad-e2e-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(manifest, output, topic: "acme-data-access-e2e", domain: "system-design").Request);
        var result = await new AnalysisEngine().AnalyzeAsync(request);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
        }
    }

    private static T Deserialize<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Deserialize(File.ReadAllBytes(path), typeInfo)
            ?? throw new InvalidOperationException($"{path} deserialized to null.");
}
