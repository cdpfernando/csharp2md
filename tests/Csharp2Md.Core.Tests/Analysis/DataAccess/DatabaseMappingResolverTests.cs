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

    // DAD-05: a HasColumnName literal mints a column node owned by the entity's mapped object.
    [Fact]
    public void Resolve_ConfiguredColumnOnConfiguredEntity_MintsAnExactColumnOwnedByTheTableNode()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"));

        var table = Assert.Single(resolution.Objects);
        var column = Assert.Single(resolution.Columns);
        Assert.Equal("order_status", column.Name);
        Assert.Equal(table.ObjectId, column.ObjectId);
        Assert.Equal(FactResolution.Exact, column.Resolution);
        Assert.Equal(DatabaseColumnFactId.Create(table.ObjectId, "order_status"), column.ColumnId);
    }

    // DAD-05: the mapping relation is sourced at the property symbol and targets the column node.
    [Fact]
    public void Resolve_ConfiguredColumn_YieldsExactMapsPropertyToColumnSourcedAtTheProperty()
    {
        var property = ResolverScenario.EntityProperty("Status", "Order");

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order"), property),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"));

        var mapping = Assert.Single(
            resolution.Relations, relation => relation.RelationKind == "maps-property-to-column");
        Assert.Equal(property.SymbolId.ToFactId(), mapping.SourceId);
        Assert.Equal(Assert.Single(resolution.Columns).ColumnId.ToFactId(), mapping.TargetId);
        Assert.Equal(FactResolution.Exact, mapping.Resolution);
        Assert.Equal("order_status", ResolverScenario.Detail(mapping, "target_text"));
        Assert.Equal("configured", ResolverScenario.Detail(mapping, "mapping"));
        Assert.Null(mapping.UnresolvedReason);
    }

    // DAD-06: an unconfigured property of a mapped entity falls back to its own name at Heuristic.
    [Fact]
    public void Resolve_UnconfiguredPropertyOfMappedEntity_YieldsHeuristicMappingNamingTheProperty()
    {
        var property = ResolverScenario.EntityProperty("Status", "Order");

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order"), property),
            ResolverScenario.EntitySet("Order", "Orders"));

        var mapping = Assert.Single(
            resolution.Relations, relation => relation.RelationKind == "maps-property-to-column");
        Assert.Equal(property.SymbolId.ToFactId(), mapping.SourceId);
        Assert.Null(mapping.TargetId);
        Assert.Equal(FactResolution.Heuristic, mapping.Resolution);
        Assert.Equal("Status", ResolverScenario.Detail(mapping, "target_text"));
        Assert.Equal("convention", ResolverScenario.Detail(mapping, "mapping"));
        Assert.Equal("convention-mapping", mapping.UnresolvedReason);
    }

    // DAD-05 and DAD-06 together: configuration decides per property, not per entity.
    [Fact]
    public void Resolve_EntityWithOneConfiguredAndOneUnconfiguredProperty_YieldsOneMappingEach()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order"),
                ResolverScenario.EntityProperty("Amount", "Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"));

        var mappings = resolution.Relations
            .Where(relation => relation.RelationKind == "maps-property-to-column")
            .ToDictionary(relation => ResolverScenario.Detail(relation, "target_text")!);

        Assert.Equal(2, mappings.Count);
        Assert.Equal(FactResolution.Exact, mappings["order_status"].Resolution);
        Assert.Equal(FactResolution.Heuristic, mappings["Amount"].Resolution);
        Assert.Null(mappings["Amount"].TargetId);
    }

    // A column node can never exist without an owning object node, so a configured column on a
    // convention-mapped entity is recorded as an unresolved mapping rather than an orphan node.
    [Fact]
    public void Resolve_ConfiguredColumnOnConventionMappedEntity_MintsNoColumnAndStaysUnresolved()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"));

        Assert.Empty(resolution.Columns);
        var mapping = Assert.Single(
            resolution.Relations, relation => relation.RelationKind == "maps-property-to-column");
        Assert.Null(mapping.TargetId);
        Assert.Equal(FactResolution.Unresolved, mapping.Resolution);
        Assert.Equal("unmapped-owning-object", mapping.UnresolvedReason);
        Assert.Equal("order_status", ResolverScenario.Detail(mapping, "target_text"));
    }

    // DAD-06 applies to a mapped entity only; a type nothing maps produces no column mappings.
    [Fact]
    public void Resolve_PropertyOfUnmappedType_YieldsNoColumnMapping()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.ConfiguredTable("Invoice", "tb_invoice"));

        Assert.DoesNotContain(
            resolution.Relations, relation => relation.RelationKind == "maps-property-to-column");
    }

    // The convention mapping is evidenced at the claim that proved the entity is mapped, which is the
    // only document guaranteed to carry a retained extent for the solution-level fragment.
    [Fact]
    public void Resolve_UnconfiguredProperty_IsEvidencedAtTheDocumentThatProvedTheEntityMapping()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.EntitySet("Order", "Orders", "Data/OrderDbContext.cs"));

        var mapping = Assert.Single(
            resolution.Relations, relation => relation.RelationKind == "maps-property-to-column");

        Assert.Equal("Data/OrderDbContext.cs", mapping.Evidence.RelativePath);
    }

    // The literal still proves the column, but a mapping with no sourceable property is not emitted.
    [Fact]
    public void Resolve_ConfiguredColumnWhosePropertyIsAbsent_MintsTheColumnButEmitsNoMapping()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"));

        Assert.Single(resolution.Columns);
        Assert.DoesNotContain(
            resolution.Relations, relation => relation.RelationKind == "maps-property-to-column");
    }

    // DAD-07: a DbSet read targets the entity's mapped object and names the precise operation.
    [Fact]
    public void Resolve_EntitySetRead_YieldsReadsSourcedAtTheMemberTargetingTheMappedObject()
    {
        var access = ResolverScenario.EntitySetAccess("Order", "Orders", DatabaseOperation.Read);

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            access);

        var reads = Assert.Single(resolution.Relations, relation => relation.RelationKind == "reads");
        Assert.Equal(access.OwnerId, reads.SourceId);
        Assert.Equal(Assert.Single(resolution.Objects).ObjectId.ToFactId(), reads.TargetId);
        Assert.Equal("read", ResolverScenario.Detail(reads, "operation"));
        Assert.Equal("Orders", ResolverScenario.Detail(reads, "target_text"));
        Assert.Null(reads.UnresolvedReason);
    }

    // DAD-14: an access whose entity is only convention-mapped is kept, not dropped.
    [Fact]
    public void Resolve_EntitySetReadOnConventionMappedEntity_KeepsTheAccessUnresolved()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.EntitySetAccess("Order", "Orders", DatabaseOperation.Read));

        var reads = Assert.Single(resolution.Relations, relation => relation.RelationKind == "reads");
        Assert.Null(reads.TargetId);
        Assert.Equal("Orders", ResolverScenario.Detail(reads, "target_text"));
        Assert.Equal(FactResolution.Heuristic, reads.Resolution);
        Assert.Equal("convention-mapping", reads.UnresolvedReason);
    }

    // DAD-10: the write family maps onto one coarse kind carrying the precise operation.
    [Theory]
    [InlineData(DatabaseOperation.Insert, "insert")]
    [InlineData(DatabaseOperation.Update, "update")]
    [InlineData(DatabaseOperation.Delete, "delete")]
    public void Resolve_EntitySetWrite_YieldsWritesCarryingThePreciseOperation(
        DatabaseOperation operation,
        string expected)
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(ResolverScenario.Entity("Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.EntitySetAccess("Order", "Orders", operation));

        var writes = Assert.Single(resolution.Relations, relation => relation.RelationKind == "writes");

        Assert.Equal(expected, ResolverScenario.Detail(writes, "operation"));
    }

    // DAD-08: a projected entity property becomes a reads-column carrying usage read.
    [Fact]
    public void Resolve_ProjectedColumn_YieldsReadsColumnTargetingTheConfiguredColumn()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"),
            ResolverScenario.LinqColumn("Order", "Status", ColumnUsage.Read));

        var read = Assert.Single(resolution.Relations, relation => relation.RelationKind == "reads-column");
        Assert.Equal("read", ResolverScenario.Detail(read, "usage"));
        Assert.Equal("Status", ResolverScenario.Detail(read, "target_text"));
        Assert.Equal(Assert.Single(resolution.Columns).ColumnId.ToFactId(), read.TargetId);
    }

    // DAD-09: a Where lambda's property becomes a filters-by carrying usage filter.
    [Fact]
    public void Resolve_FilteredColumn_YieldsFiltersByCarryingUsageFilter()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Id", "Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.LinqColumn("Order", "Id", ColumnUsage.Filter));

        var filter = Assert.Single(resolution.Relations, relation => relation.RelationKind == "filters-by");
        Assert.Equal("filter", ResolverScenario.Detail(filter, "usage"));
        Assert.Equal("Id", ResolverScenario.Detail(filter, "target_text"));
        Assert.Null(filter.TargetId);
        Assert.Equal(FactResolution.Heuristic, filter.Resolution);
        Assert.Equal("convention-mapping", filter.UnresolvedReason);
    }

    // DAD-11: a tracked write whose property names exactly one exposed entity is attributed to it.
    [Fact]
    public void Resolve_TrackedWriteMatchingOneExposedEntity_YieldsHeuristicWritesColumnOnItsColumn()
    {
        var write = ResolverScenario.TrackedWrite("Status", "order.Status");

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.ConfiguredTable("Order", "tb_order"),
            ResolverScenario.ConfiguredColumn("Order", "Status", "order_status"),
            write);

        var written = Assert.Single(
            resolution.Relations, relation => relation.RelationKind == "writes-column");
        Assert.Equal(write.OwnerId, written.SourceId);
        Assert.Equal(Assert.Single(resolution.Columns).ColumnId.ToFactId(), written.TargetId);
        Assert.Equal("write", ResolverScenario.Detail(written, "usage"));
        Assert.Equal(FactResolution.Heuristic, written.Resolution);
    }

    // DAD-12: the same property on two exposed entities is ambiguous, never a coin flip.
    [Fact]
    public void Resolve_TrackedWriteMatchingTwoExposedEntities_YieldsCandidateWithTheObservedText()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order"),
                ResolverScenario.Entity("Invoice", documentPath: "Model/Invoice.cs"),
                ResolverScenario.EntityProperty("Status", "Invoice", "Model/Invoice.cs")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.EntitySet("Invoice", "Invoices"),
            ResolverScenario.TrackedWrite("Status", "entity.Status"));

        var written = Assert.Single(
            resolution.Relations, relation => relation.RelationKind == "writes-column");
        Assert.Null(written.TargetId);
        Assert.Equal(FactResolution.Candidate, written.Resolution);
        Assert.Equal("entity.Status", ResolverScenario.Detail(written, "target_text"));
        Assert.Equal("ambiguous-entity-attribution", written.UnresolvedReason);
    }

    // Spec Edge Case: a property no exposed entity declares yields no writes-column at all.
    [Fact]
    public void Resolve_TrackedWriteMatchingNoExposedEntity_YieldsNoWritesColumn()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.TrackedWrite("Nickname", "person.Nickname"));

        Assert.DoesNotContain(
            resolution.Relations, relation => relation.RelationKind == "writes-column");
    }

    // DAD-22: a literal-proven SQL target mints a node and the access points at it.
    [Fact]
    public void Resolve_ReadableSqlAccess_MintsTheNamedObjectAndTargetsIt()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            ResolverScenario.SqlAccess("Orders", DatabaseObjectKind.Unknown, DatabaseOperation.Read));

        var node = Assert.Single(resolution.Objects);
        Assert.Equal("Orders", node.Name);
        Assert.Equal(DatabaseObjectKind.Unknown, node.Kind);
        var reads = Assert.Single(resolution.Relations, relation => relation.RelationKind == "reads");
        Assert.Equal(node.ObjectId.ToFactId(), reads.TargetId);
        Assert.Equal("read", ResolverScenario.Detail(reads, "operation"));
    }

    // DAD-23: an EXEC target is a procedure, and its access is an executes relation.
    [Fact]
    public void Resolve_ProcedureSqlAccess_MintsAProcedureNodeAndYieldsExecutes()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            ResolverScenario.SqlAccess("usp_Sync", DatabaseObjectKind.Procedure, DatabaseOperation.Execute));

        Assert.Equal(DatabaseObjectKind.Procedure, Assert.Single(resolution.Objects).Kind);
        var executes = Assert.Single(resolution.Relations, relation => relation.RelationKind == "executes");
        Assert.Equal("execute", ResolverScenario.Detail(executes, "operation"));
    }

    // DAD-27: dynamic SQL never invents a destination, and never mints a node.
    [Fact]
    public void Resolve_DynamicSqlAccess_MintsNoNodeAndStaysUnresolved()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            ResolverScenario.SqlAccess("dynamic-table", null, DatabaseOperation.Read) with
            {
                ShapeConfidence = FactResolution.Unresolved,
                UnresolvedReason = "dynamic-sql",
            });

        Assert.Empty(resolution.Objects);
        var reads = Assert.Single(resolution.Relations, relation => relation.RelationKind == "reads");
        Assert.Null(reads.TargetId);
        Assert.Equal("dynamic-table", ResolverScenario.Detail(reads, "target_text"));
        Assert.Equal(FactResolution.Unresolved, reads.Resolution);
        Assert.Equal("dynamic-sql", reads.UnresolvedReason);
    }

    // DAD-28: an unreadable target keeps the statement text as evidence a human can resolve.
    [Fact]
    public void Resolve_UnreadableSqlTarget_PreservesTheStatementAsASqlDetail()
    {
        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            ResolverScenario.SqlAccess(null, null, DatabaseOperation.Update) with
            {
                ShapeConfidence = FactResolution.Unresolved,
                UnresolvedReason = "unreadable-sql-target",
                SqlText = "UPDATE (SELECT 1) SET x = 1",
            });

        var writes = Assert.Single(resolution.Relations, relation => relation.RelationKind == "writes");
        Assert.Null(writes.TargetId);
        Assert.Equal("UPDATE (SELECT 1) SET x = 1", ResolverScenario.Detail(writes, "sql"));
        Assert.Equal("unreadable-sql-target", writes.UnresolvedReason);
    }

    // DAD-24 and DAD-25: a written SQL column becomes a writes-column on the statement's object.
    [Fact]
    public void Resolve_SqlWrittenColumn_YieldsWritesColumnOnTheStatementsObject()
    {
        var access = ResolverScenario.SqlAccess("Orders", DatabaseObjectKind.Unknown, DatabaseOperation.Insert);

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            access,
            ResolverScenario.SqlColumn(access, "Status", ColumnUsage.Write));

        var column = Assert.Single(resolution.Columns);
        Assert.Equal("Status", column.Name);
        Assert.Equal(Assert.Single(resolution.Objects).ObjectId, column.ObjectId);
        var written = Assert.Single(resolution.Relations, relation => relation.RelationKind == "writes-column");
        Assert.Equal(column.ColumnId.ToFactId(), written.TargetId);
        Assert.Equal("write", ResolverScenario.Detail(written, "usage"));
    }

    // DAD-26: a filtered SQL column becomes a filters-by carrying usage filter.
    [Fact]
    public void Resolve_SqlFilterColumn_YieldsFiltersByCarryingUsageFilter()
    {
        var access = ResolverScenario.SqlAccess("Orders", DatabaseObjectKind.Unknown, DatabaseOperation.Read);

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            access,
            ResolverScenario.SqlColumn(access, "Id", ColumnUsage.Filter));

        var filter = Assert.Single(resolution.Relations, relation => relation.RelationKind == "filters-by");
        Assert.Equal("filter", ResolverScenario.Detail(filter, "usage"));
        Assert.Equal("Id", ResolverScenario.Detail(filter, "target_text"));
    }

    // A column of an unresolved statement has no object to hang on, so it mints nothing.
    [Fact]
    public void Resolve_SqlColumnOfUnresolvedStatement_MintsNoColumnAndKeepsTheRelation()
    {
        var access = ResolverScenario.SqlAccess(null, null, DatabaseOperation.Update) with
        {
            ShapeConfidence = FactResolution.Unresolved,
            UnresolvedReason = "unreadable-sql-target",
        };

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(),
            access,
            ResolverScenario.SqlColumn(access, "Status", ColumnUsage.Write) with
            {
                ObjectText = null,
                ShapeConfidence = FactResolution.Unresolved,
            });

        Assert.Empty(resolution.Columns);
        var written = Assert.Single(resolution.Relations, relation => relation.RelationKind == "writes-column");
        Assert.Null(written.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(written.UnresolvedReason));
    }

    // DAD-14: no relation ever leaves pass two with a null target and no stated reason.
    [Fact]
    public void Resolve_MixedSnapshot_LeavesEveryUntargetedRelationWithAReason()
    {
        var access = ResolverScenario.SqlAccess("Orders", DatabaseObjectKind.Unknown, DatabaseOperation.Read);

        var resolution = ResolverScenario.Resolve(
            ResolverScenario.Symbols(
                ResolverScenario.Entity("Order"),
                ResolverScenario.EntityProperty("Status", "Order")),
            ResolverScenario.EntitySet("Order", "Orders"),
            ResolverScenario.EntitySetAccess("Order", "Orders", DatabaseOperation.Read),
            ResolverScenario.LinqColumn("Order", "Status", ColumnUsage.Read),
            ResolverScenario.TrackedWrite("Status", "order.Status"),
            access,
            ResolverScenario.SqlColumn(access, "Id", ColumnUsage.Filter));

        Assert.NotEmpty(resolution.Relations.Where(relation => relation.TargetId is null));
        Assert.All(
            resolution.Relations.Where(relation => relation.TargetId is null),
            relation => Assert.False(string.IsNullOrWhiteSpace(relation.UnresolvedReason)));
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

    public static SymbolFact EntityProperty(
        string name,
        string containingType,
        string documentPath = "Model/Order.cs") =>
        Symbol(name, $"{containingType}.{name}", "property", documentPath, containingType);

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

    public static RawDatabaseClaim ConfiguredColumn(
        string entityName,
        string propertyName,
        string columnName,
        string documentPath = "Data/OrderConfiguration.cs",
        int line = 7) =>
        Claim(DatabaseClaimKind.ColumnConfigured, documentPath, line) with
        {
            ShapeConfidence = FactResolution.Exact,
            EntityText = entityName,
            PropertyText = propertyName,
            ColumnText = columnName,
        };

    /// <summary>A DbSet read or write, as <c>EfCoreAnalyzer</c> claims one.</summary>
    public static RawDatabaseClaim EntitySetAccess(
        string entityName,
        string setName,
        DatabaseOperation operation,
        string documentPath = "Services/OrderService.cs",
        int line = 11) =>
        Claim(DatabaseClaimKind.Access, documentPath, line) with
        {
            EntityText = entityName,
            PropertyText = setName,
            Operation = operation,
        };

    /// <summary>An entity property a LINQ chain referenced, as <c>EfCoreAnalyzer</c> claims one.</summary>
    public static RawDatabaseClaim LinqColumn(
        string entityName,
        string propertyName,
        ColumnUsage usage,
        string documentPath = "Services/OrderService.cs",
        int line = 12) =>
        Claim(DatabaseClaimKind.ColumnAccess, documentPath, line) with
        {
            EntityText = entityName,
            PropertyText = propertyName,
            Usage = usage,
        };

    /// <summary>
    /// A property assignment inside a member that also calls SaveChanges. The entity is deliberately
    /// absent: syntax cannot prove which entity the receiver held, so attribution is pass two's job.
    /// </summary>
    public static RawDatabaseClaim TrackedWrite(
        string propertyName,
        string receiverText,
        string documentPath = "Services/OrderService.cs",
        int line = 20) =>
        Claim(DatabaseClaimKind.ColumnAccess, documentPath, line) with
        {
            PropertyText = propertyName,
            ColumnText = receiverText,
            Usage = ColumnUsage.Write,
        };

    public static RawDatabaseClaim SqlAccess(
        string? objectText,
        DatabaseObjectKind? objectKind,
        DatabaseOperation operation,
        string documentPath = "Data/OrderQueries.cs",
        int line = 9) =>
        Claim(DatabaseClaimKind.Access, documentPath, line) with
        {
            AnalyzerId = DataAccessAnalyzerId.Create("csharp2md.dataaccess.sql"),
            ObjectText = objectText,
            ObjectKind = objectKind,
            Operation = operation,
        };

    /// <summary>A column of <paramref name="access"/>, carrying that statement's evidence and target.</summary>
    public static RawDatabaseClaim SqlColumn(
        RawDatabaseClaim access,
        string columnName,
        ColumnUsage usage) =>
        access with
        {
            Kind = DatabaseClaimKind.ColumnAccess,
            ColumnText = columnName,
            Usage = usage,
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
