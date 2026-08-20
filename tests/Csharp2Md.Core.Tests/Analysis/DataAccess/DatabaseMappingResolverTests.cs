using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class DatabaseMappingResolverTests
{
    // DAD-03: a ToTable literal is the only thing that mints a database object node.
    [Fact]
    public void Resolve_ConfiguredTable_MintsOneExactTableNodeBearingTheLiteral()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"));

        var node = Assert.Single(resolution.Objects);
        Assert.Equal("tb_order", node.Name);
        Assert.Equal(DatabaseObjectKind.Table, node.Kind);
        Assert.Equal(FactResolution.Exact, node.Resolution);
        Assert.Equal(
            DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "tb_order"),
            node.ObjectId);
    }

    // DAD-02 / AD-016: a convention name is never proven, so it never becomes a node.
    [Fact]
    public void Resolve_EntitySetWithoutConfiguration_MintsNoNode()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders"));

        Assert.Empty(resolution.Objects);
    }

    // DAD-02: the convention mapping is emitted, sourced at the entity, naming the DbSet property.
    [Fact]
    public void Resolve_EntitySetWithoutConfiguration_YieldsHeuristicMapsToNamingTheSetWithNoTarget()
    {
        var entity = ResolverScenario.Entity("Order");

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(entity),
            ResolverScenario.EntitySet("Order", "Orders"));

        var mapsTo = Assert.Single(resolution.Relations, relation => relation.RelationKind == "maps-to");
        Assert.Equal(entity.SymbolId.ToFactId(), mapsTo.SourceId);
        Assert.Null(mapsTo.TargetId);
        Assert.Equal(FactResolution.Heuristic, mapsTo.Resolution);
        Assert.Equal("Orders", ResolverScenario.Detail(mapsTo, "target_text"));
        Assert.Equal("convention", ResolverScenario.Detail(mapsTo, "mapping"));
        Assert.Equal("convention-mapping", mapsTo.UnresolvedReason);
    }

    // DAD-03: the case that forced the two-pass design - the ToTable lives in another document.
    [Fact]
    public void Resolve_TableConfiguredInAnotherDocument_ResolvesTheEntityToThatNode()
    {
        var entity = ResolverScenario.Entity("Order");

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(entity),
            ResolverScenario.EntitySet("Order", "Orders", "Data/OrderDbContext.cs"),
            ResolverScenario.ConfiguredTable("Order", "tb_order", "Data/OrderConfiguration.cs"));

        var mapsTo = Assert.Single(resolution.Relations, relation => relation.RelationKind == "maps-to");
        Assert.Equal(entity.SymbolId.ToFactId(), mapsTo.SourceId);
        Assert.Equal(Assert.Single(resolution.Objects).ObjectId.ToFactId(), mapsTo.TargetId);
        Assert.Equal(FactResolution.Exact, mapsTo.Resolution);
        Assert.Equal("configured", ResolverScenario.Detail(mapsTo, "mapping"));
        Assert.Equal("tb_order", ResolverScenario.Detail(mapsTo, "target_text"));
        Assert.Null(mapsTo.UnresolvedReason);
    }

    // DAD-13: the configured mapping is evidenced at the document that carries the configuration.
    [Fact]
    public void Resolve_TableConfiguredInAnotherDocument_CarriesTheConfigurationDocumentAsEvidence()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders", "Data/OrderDbContext.cs"),
            ResolverScenario.ConfiguredTable("Order", "tb_order", "Data/OrderConfiguration.cs"));

        var mapsTo = Assert.Single(resolution.Relations, relation => relation.RelationKind == "maps-to");

        Assert.Equal("Data/OrderConfiguration.cs", mapsTo.Evidence.RelativePath);
    }

    // DAD-04: configured beats convention - only the configured mapping survives.
    [Fact]
    public void Resolve_EntityWithBothConventionAndConfiguration_YieldsOnlyTheConfiguredMapping()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.ConfiguredTable("Order", "tb_order", "Data/OrderConfiguration.cs"));

        var mapsTo = Assert.Single(resolution.Relations, relation => relation.RelationKind == "maps-to");

        Assert.Equal("configured", ResolverScenario.Detail(mapsTo, "mapping"));
    }

    // DAD-04: precedence cannot depend on which document was seen first.
    [Fact]
    public void Resolve_ConfigurationSeenBeforeTheEntitySet_StillYieldsOnlyTheConfiguredMapping()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order", "Data/AaaConfiguration.cs"),
            ResolverScenario.EntitySet("Order", "Orders", "Data/ZzzDbContext.cs"));

        var mapsTo = Assert.Single(resolution.Relations, relation => relation.RelationKind == "maps-to");

        Assert.Equal("configured", ResolverScenario.Detail(mapsTo, "mapping"));
        Assert.Equal(FactResolution.Exact, mapsTo.Resolution);
    }

    // DAD-01: one exposes per DbSet property, sourced at the declaring context, naming the entity.
    [Fact]
    public void Resolve_EntitySet_YieldsExposesSourcedAtTheContextNamingTheEntity()
    {
        var claim = ResolverScenario.EntitySet("Order", "Orders");

        var resolution = ResolverScenario.Resolve(ResolverScenario.Symbols(ResolverScenario.Entity("Order")), claim);

        var exposes = Assert.Single(resolution.Relations, relation => relation.RelationKind == "exposes");
        Assert.Equal(claim.OwnerId, exposes.SourceId);
        Assert.Null(exposes.TargetId);
        Assert.Equal("Order", ResolverScenario.Detail(exposes, "target_text"));
        Assert.False(string.IsNullOrWhiteSpace(exposes.UnresolvedReason));
    }

    // Spec Edge Case: two contexts exposing the same entity produce one exposes each.
    [Fact]
    public void Resolve_TwoContextsExposingTheSameEntity_YieldOneExposesPerContext()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders", "Data/OrderDbContext.cs"),
            ResolverScenario.EntitySet("Order", "LegacyOrders", "Data/LegacyDbContext.cs"));

        var exposes = resolution.Relations.Where(relation => relation.RelationKind == "exposes").ToArray();

        Assert.Equal(2, exposes.Length);
        Assert.Equal(2, exposes.Select(relation => relation.SourceId).Distinct().Count());
    }

    // Spec Edge Case: with no configuration, one convention mapping per distinct DbSet property name.
    [Fact]
    public void Resolve_TwoContextsExposingTheSameEntityUnconfigured_YieldOneMapsToPerDistinctSetName()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders", "Data/OrderDbContext.cs"),
            ResolverScenario.EntitySet("Order", "LegacyOrders", "Data/LegacyDbContext.cs"));

        var mapsTo = resolution.Relations.Where(relation => relation.RelationKind == "maps-to").ToArray();

        Assert.Equal(
            ["LegacyOrders", "Orders"],
            mapsTo.Select(relation => ResolverScenario.Detail(relation, "target_text")).Order(StringComparer.Ordinal));
    }

    // Spec Edge Case: with configuration present, the two contexts share the single configured mapping.
    [Fact]
    public void Resolve_TwoContextsExposingTheSameConfiguredEntity_YieldOneConfiguredMapsTo()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders", "Data/OrderDbContext.cs"),
            ResolverScenario.EntitySet("Order", "LegacyOrders", "Data/LegacyDbContext.cs"),
            ResolverScenario.ConfiguredTable("Order", "tb_order", "Data/OrderConfiguration.cs"));

        var mapsTo = Assert.Single(resolution.Relations, relation => relation.RelationKind == "maps-to");

        Assert.Equal("configured", ResolverScenario.Detail(mapsTo, "mapping"));
    }

    // The relation needs a source the fragment can reference; the literal still proves the node.
    [Fact]
    public void Resolve_EntityTypeAbsentFromTheRun_MintsTheNodeButEmitsNoMapsTo()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            ResolverScenario.ConfiguredTable("Order", "tb_order"));

        Assert.Single(resolution.Objects);
        Assert.DoesNotContain(resolution.Relations, relation => relation.RelationKind == "maps-to");
    }

    // Two types share the entity's simple name, so no single symbol can source the mapping.
    [Fact]
    public void Resolve_EntityNameMatchingTwoTypes_EmitsNoMapsTo()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order", "Acme.Orders.Order", "Orders/Order.cs"),
                ResolverScenario.Entity("Order", "Acme.Legacy.Order", "Legacy/Order.cs")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"));

        Assert.DoesNotContain(resolution.Relations, relation => relation.RelationKind == "maps-to");
    }

    // Two documents configuring the same table converge on one node carrying both evidence spans.
    [Fact]
    public void Resolve_SameTableConfiguredTwice_YieldsOneNodeCarryingBothEvidenceSpans()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order"), ResolverScenario.Entity("Archive")),
            ResolverScenario.ConfiguredTable("Order", "tb_order", "Data/OrderConfiguration.cs"),
            ResolverScenario.ConfiguredTable("Archive", "tb_order", "Data/ArchiveConfiguration.cs"));

        var node = Assert.Single(resolution.Objects);

        Assert.Equal(2, node.Evidence.Length);
        Assert.Equal(
            ["Data/ArchiveConfiguration.cs", "Data/OrderConfiguration.cs"],
            node.Evidence.Select(evidence => evidence.RelativePath).Order(StringComparer.Ordinal));
    }
}

/// <summary>
/// Builds the pass-one claim snapshots and symbol indexes the resolver tests run against, so each test
/// states only the shape it is about.
/// </summary>
internal static class ResolverScenario
{
    public static ProjectFactId ProjectId { get; } = ProjectFactId.Create("src/App/App.csproj");

    public static DatabaseResolution Resolve(ISymbolIndex symbols, params RawDatabaseClaim[] claims) =>
        DatabaseMappingResolver.Resolve(Snapshot(claims), symbols);

    public static DatabaseClaimSnapshot Snapshot(params RawDatabaseClaim[] claims)
    {
        var accumulator = new DatabaseClaimAccumulator();
        foreach (var group in claims.GroupBy(claim => claim.Evidence.RelativePath, StringComparer.Ordinal))
        {
            var lineCount = group.Max(claim => claim.Evidence.EndLine);
            accumulator.Add(
                DocumentFactId.Create(ProjectId, group.Key),
                group.Key,
                Enumerable.Repeat(200, lineCount).ToImmutableArray(),
                [.. group]);
        }

        return accumulator.ToSnapshot();
    }

    public static ISymbolIndex Symbols(params SymbolFact[] symbols) =>
        SymbolIndexBuilder.Build(symbols, [], [], []);

    public static SymbolFact Entity(
        string name,
        string? fullyQualifiedName = null,
        string documentPath = "Model/Order.cs") =>
        Symbol(name, fullyQualifiedName ?? name, "class", documentPath, containingType: null);

    public static string? Detail(ResolvedDatabaseRelation relation, string key) =>
        relation.Details.SingleOrDefault(detail => detail.Key == key).Value;

    public static RawDatabaseClaim EntitySet(
        string entityName,
        string setName,
        string documentPath = "Data/OrderDbContext.cs",
        int line = 3) =>
        Claim(DatabaseClaimKind.EntitySetExposed, documentPath, line) with
        {
            OwnerId = ContextOwner(documentPath),
            EntityText = entityName,
            PropertyText = setName,
        };

    public static RawDatabaseClaim ConfiguredTable(
        string entityName,
        string tableName,
        string documentPath = "Data/OrderConfiguration.cs",
        int line = 5) =>
        Claim(DatabaseClaimKind.TableConfigured, documentPath, line) with
        {
            ShapeConfidence = FactResolution.Exact,
            EntityText = entityName,
            ObjectText = tableName,
            ObjectKind = DatabaseObjectKind.Table,
        };

    public static RawDatabaseClaim Claim(DatabaseClaimKind kind, string documentPath, int line) => new()
    {
        Kind = kind,
        OwnerId = MemberOwner(documentPath),
        Evidence = new Evidence(DocumentFactId.Create(ProjectId, documentPath), documentPath, line, 1, line, 40),
        ShapeConfidence = FactResolution.Syntactic,
        AnalyzerId = DataAccessAnalyzerId.Create("csharp2md.dataaccess.efcore"),
    };

    public static FactId MemberOwner(string documentPath) =>
        SymbolFactId.CreateSyntactic(ProjectId, documentPath, "method", $"void Configure_{Slug(documentPath)}()").ToFactId();

    public static FactId ContextOwner(string documentPath) =>
        SymbolFactId.CreateSyntactic(ProjectId, documentPath, "class", $"class Context_{Slug(documentPath)}").ToFactId();

    private static string Slug(string documentPath) =>
        documentPath.Replace('/', '_').Replace('.', '_');

    private static SymbolFact Symbol(
        string name,
        string fullyQualifiedName,
        string kind,
        string documentPath,
        string? containingType)
    {
        var id = SymbolFactId.CreateSyntactic(ProjectId, documentPath, kind, fullyQualifiedName);
        return new SymbolFact(
            FactHeader.Create(id.ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
            id,
            DocumentFactId.Create(ProjectId, documentPath),
            kind,
            ContainsErrorSymbol: false,
            [],
            [],
            [],
            Semantics: null,
            Name: name,
            FullyQualifiedName: fullyQualifiedName,
            Namespace: null,
            ContainingType: containingType,
            ContainingSymbolId: null,
            Signature: $"{kind} {fullyQualifiedName}",
            Arity: 0,
            ParameterTypes: []);
    }
}
