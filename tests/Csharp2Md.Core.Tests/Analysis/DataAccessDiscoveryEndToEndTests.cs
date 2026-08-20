using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// Both of spec.md's P1 Independent Tests - the EF Core one and the literal-SQL one - restated against a
/// real default-mode <see cref="AnalysisEngine.AnalyzeAsync"/> run over <c>fixtures/SyntheticSolution</c>,
/// in the fixture-and-run structure <c>RelationCollectorEndToEndTests</c> uses. Every assertion here maps
/// to a <c>DAD-NN</c> acceptance criterion, so the criteria are proven against the emitted files rather
/// than against a hand-built resolver input. The secret-absence invariant and the determinism guarantee
/// are pinned over the same run.
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

    // DAD-21: the verb decides the coarse direction and the precise operation, for every shape the
    // fixture's SQL document carries.
    [Theory]
    [InlineData("SelectOrder", "reads", "read")]
    [InlineData("InsertOrder", "writes", "insert")]
    [InlineData("UpdateOrderStatus", "writes", "update")]
    [InlineData("RebuildTotals", "executes", "execute")]
    [InlineData("DeleteArchived", "writes", "delete")]
    public void DAD21_RecognisedVerb_DerivesTheAccessKindAndOperationFromTheStatement(
        string member, string relationKind, string operation)
    {
        var access = Assert.Single(fixture.DataRelations, relation =>
            AccessKinds.Contains(relation.RelationKind) && SourcedAt(relation, member));

        Assert.Equal(relationKind, access.RelationKind);
        Assert.Equal(operation, Detail(access, "operation"));
    }

    // DAD-22: a plain identifier after the verb's anchor keyword is proof of a name, so it mints a node
    // and the access points at it.
    [Theory]
    [InlineData("SelectOrder")]
    [InlineData("InsertOrder")]
    [InlineData("UpdateOrderStatus")]
    public void DAD22_ReadableTargetIdentifier_MintsAnExactNodeTheAccessPointsAt(string member)
    {
        var orders = Assert.Single(fixture.Database.Objects, node => node.Name == "Orders");
        Assert.Equal("exact", orders.Header.Resolution);

        var access = Assert.Single(fixture.DataRelations, relation =>
            AccessKinds.Contains(relation.RelationKind) && SourcedAt(relation, member));

        Assert.Equal(orders.ObjectId, access.TargetId);
        Assert.Equal("Orders", Detail(access, "target_text"));
    }

    // DAD-23: EXEC proves a procedure; no other verb proves what kind of object it touched.
    [Fact]
    public void DAD23_ExecVerb_MintsAProcedureNode_AndEveryOtherVerbMintsAnUnknownKind()
    {
        var procedure = Assert.Single(fixture.Database.Objects, node => node.Name == "usp_RebuildOrderTotals");
        Assert.Equal("procedure", procedure.Kind);

        var orders = Assert.Single(fixture.Database.Objects, node => node.Name == "Orders");
        Assert.Equal("unknown", orders.Kind);

        var execute = Assert.Single(fixture.DataRelations, relation => relation.RelationKind == "executes");
        Assert.Equal(procedure.ObjectId, execute.TargetId);
    }

    // DAD-24: the INSERT column list is proof of every column it names.
    [Fact]
    public void DAD24_InsertColumnList_EmitsOneWritesColumnPerListedColumn()
    {
        var columns = fixture.DataRelations
            .Where(relation => relation.RelationKind == "writes-column" && SourcedAt(relation, "InsertOrder"))
            .ToArray();

        Assert.Equal(
            ["Amount", "Id", "Status"],
            columns.Select(relation => Detail(relation, "target_text")).Order(StringComparer.Ordinal));
        Assert.All(columns, relation => Assert.Equal("write", Detail(relation, "usage")));
        Assert.All(columns, relation => Assert.NotNull(relation.TargetId));
    }

    // DAD-25: the UPDATE ... SET assignment list is proof of every column it assigns, and of nothing else.
    [Fact]
    public void DAD25_UpdateSetList_EmitsOneWritesColumnPerAssignedColumn()
    {
        var column = Assert.Single(fixture.DataRelations, relation =>
            relation.RelationKind == "writes-column" && SourcedAt(relation, "UpdateOrderStatus"));

        Assert.Equal("Status", Detail(column, "target_text"));
        Assert.Equal("write", Detail(column, "usage"));
        Assert.NotNull(column.TargetId);
    }

    // DAD-26: both readable right-hand sides the criterion names - a parameter and a literal.
    [Theory]
    [InlineData("SelectOrder", "Id")]
    [InlineData("DeleteArchived", "Status")]
    public void DAD26_WhereComparisonAgainstALiteralOrParameter_EmitsAFiltersByForThatColumn(
        string member, string column)
    {
        var filter = Assert.Single(fixture.DataRelations, relation =>
            relation.RelationKind == "filters-by" && SourcedAt(relation, member));

        Assert.Equal(column, Detail(filter, "target_text"));
        Assert.Equal("filter", Detail(filter, "usage"));
    }

    // DAD-27: an interpolated statement proves a verb and nothing else. Its access survives; no node is
    // invented for the table it does not name.
    [Fact]
    public void DAD27_InterpolatedStatement_StaysUnresolvedWithNoNodeAndItsSqlPreserved()
    {
        var dynamicAccess = Assert.Single(fixture.DataRelations, relation => SourcedAt(relation, "SelectAllFrom"));

        Assert.Equal("reads", dynamicAccess.RelationKind);
        Assert.Equal("unresolved", dynamicAccess.Header.Resolution);
        Assert.Null(dynamicAccess.TargetId);
        Assert.Equal("dynamic-sql", dynamicAccess.UnresolvedReason);
        Assert.Equal("dynamic-table", Detail(dynamicAccess, "target_text"));
        Assert.Equal("$\"SELECT * FROM {tableName}\"", Detail(dynamicAccess, "sql"));
        Assert.DoesNotContain(
            fixture.Database.Objects,
            node => node.Name is "dynamic-table" or "tableName" or "{tableName}");
    }

    // DAD-28: the verb is readable and the target is not, so the statement itself is what survives.
    [Fact]
    public void DAD28_ReadableVerbWithUnreadableTarget_StaysUnresolvedWithItsStatementPreserved()
    {
        var access = Assert.Single(fixture.DataRelations, relation =>
            AccessKinds.Contains(relation.RelationKind) && SourcedAt(relation, "DeleteArchived"));

        Assert.Equal("unresolved", access.Header.Resolution);
        Assert.Null(access.TargetId);
        Assert.Equal("unreadable-sql-target", access.UnresolvedReason);
        Assert.Equal("DELETE FROM [Orders] WHERE Status = 'Archived'", Detail(access, "sql"));
        Assert.DoesNotContain(fixture.Database.Objects, node => node.Name == "[Orders]");
    }

    /// <summary>
    /// DAD-15. The fixture carries two different credential values on purpose, and each one proves a
    /// different half of the invariant: the one that never enters a C# document must reach no output file
    /// at all, and the one the SQL analyser genuinely walks past must reach no relation detail, no node
    /// and no diagnostic. The second value does survive in the two places the tool reproduces source
    /// verbatim, which is what the tool is for; DAD-15 governs the facts this stage synthesises.
    /// </summary>
    [Fact]
    public void DAD15_NoCredentialText_ReachesTheFactsTheDiscoveryStageProduces()
    {
        // Without a credential in the analysed input, every assertion below would hold of an
        // implementation with no guard at all.
        Assert.Contains(
            ConfigCredential,
            File.ReadAllText(TestPaths.SyntheticSolution(Path.Combine("Acme.Orders", "appsettings.json"))),
            StringComparison.Ordinal);
        Assert.Contains(
            InlineCredential,
            File.ReadAllText(TestPaths.SyntheticSolution(Path.Combine("Acme.Orders", "Data", "OrderSqlQueries.cs"))),
            StringComparison.Ordinal);

        Assert.Empty(FilesContaining(EveryOutputFile(fixture.Output), ConfigCredential));

        var discoveryOutputs = DiscoveryOutputs(TopicLayout.RawRoot(fixture.Output));
        Assert.Empty(FilesContaining(discoveryOutputs, InlineCredential));
        Assert.Empty(FilesContaining(discoveryOutputs, "Password="));
        Assert.DoesNotContain(
            fixture.DataRelations.SelectMany(relation => relation.Details ?? []),
            detail => detail.Value.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    // DAD-20: the same unchanged input analysed twice writes the same persistence bytes.
    [Fact]
    public void DAD20_TwoRunsOverUnchangedInput_ProduceByteIdenticalPersistenceOutput()
    {
        var rawA = TopicLayout.RawRoot(fixture.Output);
        var rawB = TopicLayout.RawRoot(fixture.OutputB);

        var relativeA = PersistenceFiles(rawA);
        var relativeB = PersistenceFiles(rawB);

        Assert.NotEmpty(relativeA);
        Assert.Equal(relativeA, relativeB);
        foreach (var relative in relativeA)
        {
            var bytesA = File.ReadAllBytes(Path.Combine(rawA, relative));
            var bytesB = File.ReadAllBytes(Path.Combine(rawB, relative));
            Assert.True(bytesA.AsSpan().SequenceEqual(bytesB), $"persistence output differs between runs: {relative}");
        }
    }

    /// <summary>DAD-15: the fixture's credential value that only ever lives in configuration.</summary>
    private const string ConfigCredential = "appsettings-fixture-secret";

    /// <summary>DAD-15: the fixture's credential value the SQL analyser walks past in C# source.</summary>
    private const string InlineCredential = "inline-fixture-secret";

    /// <summary>The relation kinds an access can be emitted under, per design.md's relation table.</summary>
    private static readonly string[] AccessKinds = ["reads", "writes", "executes", "accesses"];

    /// <summary>Everything the discovery stage writes: the partitions, the catalogue, its fragment, and
    /// the diagnostics any analyser could have raised.</summary>
    private static string[] DiscoveryOutputs(string raw) =>
    [
        Path.Combine(raw, "facts", "database.json"),
        Path.Combine(raw, "facts", "diagnostics.json"),
        .. Directory.EnumerateFiles(Path.Combine(raw, "facts", "relations"), "*.json"),
        .. Directory.EnumerateFiles(
            Path.Combine(raw, "facts", "database-column"), "*.json", SearchOption.AllDirectories),
    ];

    /// <summary>The persistence files DAD-20 pins, as paths relative to the raw root.</summary>
    private static string[] PersistenceFiles(string raw) =>
    [
        Path.Combine("facts", "relations", "data.json"),
        Path.Combine("facts", "database.json"),
        .. Directory.EnumerateFiles(
                Path.Combine(raw, "facts", "database-column"), "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(raw, path))
            .Order(StringComparer.Ordinal),
    ];

    private static IEnumerable<string> EveryOutputFile(string outputRoot) =>
        Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories);

    private static string[] FilesContaining(IEnumerable<string> files, string text) =>
        files.Where(path => File.ReadAllText(path).Contains(text, StringComparison.Ordinal)).ToArray();

    private static bool SourcedAt(RelationFactJson relation, string memberName) =>
        relation.SourceId.Contains(memberName, StringComparison.Ordinal);

    private static string? Detail(RelationFactJson relation, string key) =>
        relation.Details?.FirstOrDefault(detail => detail.Key == key)?.Value;
}

/// <summary>
/// Two default-mode analysis runs over the same three restorable fixture services, plus typed access to
/// the files the persistence stage writes. The second run exists for DAD-20: it is the same unchanged
/// input, so its persistence bytes must match the first run's exactly.
/// </summary>
public sealed class DataAccessDiscoveryFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-dad-e2e-{Guid.NewGuid():N}");

    public string OutputB { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-dad-e2e-{Guid.NewGuid():N}");

    public ImmutableArray<RelationFactJson> DataRelations { get; private set; }

    internal DatabaseAggregate Database { get; private set; } = null!;

    internal FactualManifest Manifest { get; private set; } = null!;

    public ImmutableArray<string> AnalysedDocuments { get; private set; }

    public async Task InitializeAsync()
    {
        await RunAsync(Output);
        await RunAsync(OutputB);

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
        foreach (var output in new[] { Output, OutputB })
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
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
