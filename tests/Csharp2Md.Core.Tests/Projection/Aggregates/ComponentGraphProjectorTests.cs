using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class ComponentGraphProjectorTests
{
    private static readonly ProjectFactId Orders = ProjectFactId.Create("src/Orders/Orders.csproj");
    private static readonly ProjectFactId Payments = ProjectFactId.Create("src/Payments/Payments.csproj");
    private static readonly DocumentFactId Document = DocumentFactId.Create(Orders, "Client.cs");
    private static readonly DetectorId Detector = DetectorId.Create("io.csharp2md.test");
    private static readonly DatabaseObjectFactId OrdersTable =
        DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "orders");
    private static readonly DatabaseColumnFactId OrdersIdColumn = DatabaseColumnFactId.Create(OrdersTable, "id");

    // COMP-10: two relation endpoints mapping to two different nodes yield exactly one edge, carrying the
    // partition, kind and node labels the relation and its endpoints actually have.
    [Fact]
    public void Project_RelationWithTwoDifferentNodeEndpoints_YieldsOneEdge()
    {
        var relation = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls");

        var result = ComponentGraphProjector.Project([Validated(relation)], Nodes());

        var edge = Assert.Single(result.Edges);
        Assert.Equal("Orders", edge.Source.Label);
        Assert.Equal("Payments", edge.Target.Label);
        Assert.Equal(RelationPartition.Structural, edge.Partition);
        Assert.Equal("calls", edge.RelationKind);
        Assert.Equal(1, edge.Count);
    }

    // COMP-13: a relation with a null TargetId never reaches the edge set.
    [Fact]
    public void Project_RelationWithNullTargetId_IsDropped()
    {
        var relation = Relation(Orders.ToFactId(), null, RelationPartition.Http, "http-request");

        var result = ComponentGraphProjector.Project([Validated(relation)], Nodes());

        Assert.Empty(result.Edges);
    }

    // COMP-14: a target that maps to no node (here, a real project id GraphNodeIndex was never given)
    // drops the relation from the edge set.
    [Fact]
    public void Project_RelationWhoseTargetResolvesToNoNode_IsDropped()
    {
        var unknownTarget = ProjectFactId.Create("src/Unknown/Unknown.csproj").ToFactId();
        var relation = Relation(Orders.ToFactId(), unknownTarget, RelationPartition.Structural, "calls");

        var result = ComponentGraphProjector.Project([ValidatedWithExtraKnownIds([unknownTarget], relation)], Nodes());

        Assert.Empty(result.Edges);
    }

    // COMP-12: a relation whose source and target resolve to the same node is dropped as a self-edge.
    [Fact]
    public void Project_RelationWhoseEndpointsResolveToTheSameNode_IsDropped()
    {
        var relation = Relation(Orders.ToFactId(), Orders.ToFactId(), RelationPartition.Structural, "calls");

        var result = ComponentGraphProjector.Project([Validated(relation)], Nodes());

        Assert.Empty(result.Edges);
    }

    // COMP-11/COMP-19: three relations sharing (source, target, partition, kind) collapse to one edge
    // whose Count is the exact number collapsed - not 1 and not left as three edges.
    [Fact]
    public void Project_ThreeRelationsSharingSourceTargetPartitionAndKind_CollapseToOneEdgeWithCountThree()
    {
        var first = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls", ordinal: 1);
        var second = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls", ordinal: 2);
        var third = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls", ordinal: 3);

        var result = ComponentGraphProjector.Project([Validated(first, second, third)], Nodes());

        var edge = Assert.Single(result.Edges);
        Assert.Equal(3, edge.Count);
    }

    // COMP-19: two relations that agree on source, target and partition but differ in kind stay two
    // distinct edges - kind is part of the dedupe key, not folded away.
    [Fact]
    public void Project_TwoRelationsDifferingOnlyInKind_StayTwoEdges()
    {
        var calls = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls");
        var creates = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "creates");

        var result = ComponentGraphProjector.Project([Validated(calls, creates)], Nodes());

        Assert.Equal(2, result.Edges.Length);
        Assert.Contains(result.Edges, edge => edge.RelationKind == "calls");
        Assert.Contains(result.Edges, edge => edge.RelationKind == "creates");
    }

    // COMP-21/COMP-22: a relation targeting a database column folds into the same dedupe group as a
    // relation of the same kind targeting the column's owning object - the count reflects both.
    [Fact]
    public void Project_ColumnTargetedRelation_FoldsIntoTheSameGroupAsAnObjectTargetedRelationOfTheSameKind()
    {
        var objectTargeted = Relation(Orders.ToFactId(), OrdersTable.ToFactId(), RelationPartition.Data, "writes-column", ordinal: 1);
        var columnTargeted = Relation(Orders.ToFactId(), OrdersIdColumn.ToFactId(), RelationPartition.Data, "writes-column", ordinal: 2);

        var result = ComponentGraphProjector.Project([Validated(objectTargeted, columnTargeted)], Nodes());

        var edge = Assert.Single(result.Edges);
        Assert.Equal("orders", edge.Target.Label);
        Assert.Equal(2, edge.Count);
    }

    // COMP-17/COMP-20/COMP-23: a project renders as a positional rectangle node and a database object as
    // a positional cylinder node, each labelled with its own Name.
    [Fact]
    public void Project_OneEdgeToADatabaseObject_RendersTheProjectRectangleAndTheDatabaseCylinderNodeLines()
    {
        var relation = Relation(Orders.ToFactId(), OrdersTable.ToFactId(), RelationPartition.Data, "writes-column");

        var result = ComponentGraphProjector.Project([Validated(relation)], Nodes());

        Assert.Contains("[\"Orders\"]", result.Mermaid, StringComparison.Ordinal);
        Assert.Contains("[(\"orders\")]", result.Mermaid, StringComparison.Ordinal);
    }

    // COMP-11: an edge line carries the wire partition name, the relation kind and the collapsed count in
    // the "<partition>:<kind> ×<count>" label.
    [Fact]
    public void Project_FiveRelationsSharingSourceTargetPartitionAndKind_RendersOneEdgeLineWithTheCollapsedCount()
    {
        var relations = Enumerable.Range(1, 5)
            .Select(ordinal => Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls", ordinal))
            .ToArray();

        var result = ComponentGraphProjector.Project([Validated(relations)], Nodes());

        Assert.Contains("-->|structural:calls ×5|", result.Mermaid, StringComparison.Ordinal);
    }

    // COMP-15: a component with no surviving edge touching it produces no node line at all.
    [Fact]
    public void Project_ANodeTouchedByNoSurvivingEdge_ProducesNoNodeLineForIt()
    {
        var unusedId = ProjectFactId.Create("src/Unused/Unused.csproj");
        var nodes = GraphNodeIndex.Build(
            [Component("project", Orders), Component("project", Payments), Component("project", unusedId)],
            [Project(Orders, "Orders"), Project(Payments, "Payments"), Project(unusedId, "Unused")],
            [],
            [],
            [DatabaseObject()],
            [Column()]);
        var relation = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls");

        var result = ComponentGraphProjector.Project([Validated(relation)], nodes);

        Assert.DoesNotContain("Unused", result.Mermaid, StringComparison.Ordinal);
    }

    // COMP-32: zero surviving edges writes exactly the empty flowchart header, with no node lines.
    [Fact]
    public void Project_NoValidatedRelations_RendersExactlyFlowchartLrWithNoNodeLines()
    {
        var result = ComponentGraphProjector.Project([], Nodes());

        Assert.Equal("flowchart LR\n", result.Mermaid);
    }

    // COMP-16: the same facts fed in two different orders write byte-identical Mermaid output - the
    // renderer's ordering, not construction order, decides the file's shape.
    [Fact]
    public void Project_SameFactsInTwoInputOrders_ProducesIdenticalMermaidOutput()
    {
        var calls = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls", ordinal: 1);
        var writes = Relation(Orders.ToFactId(), OrdersTable.ToFactId(), RelationPartition.Data, "writes-column", ordinal: 2);

        var forward = ComponentGraphProjector.Project([Validated(calls, writes)], Nodes());
        var reversed = ComponentGraphProjector.Project([Validated(writes, calls)], Nodes());

        Assert.Equal(forward.Mermaid, reversed.Mermaid);
    }

    // COMP-16/COMP-30: two database objects sharing a Name on different connections stay two distinct
    // nodes, in an order that is stable no matter which construction order encounters them first - proven
    // by building the same scenario two ways (objects and relations each supplied in the opposite order)
    // and requiring byte-identical output. Label alone is not a total order for these two nodes (both are
    // "orders"), so a renderer whose ordering keyed on Label alone would let construction order leak into
    // the result and this equality would not hold.
    [Fact]
    public void Project_TwoDatabaseObjectsSharingANameOnDifferentConnections_KeepAStableOrderNotDrivenByConstructionOrder()
    {
        var connA = DatabaseObjectFactId.Create("conn-a", DatabaseObjectKind.Table, "orders");
        var connB = DatabaseObjectFactId.Create("conn-b", DatabaseObjectKind.Table, "orders");
        var objectA = DatabaseObject(connA, "conn-a");
        var objectB = DatabaseObject(connB, "conn-b");
        var toConnA = Relation(Orders.ToFactId(), connA.ToFactId(), RelationPartition.Data, "reads", ordinal: 1);
        var toConnB = Relation(Orders.ToFactId(), connB.ToFactId(), RelationPartition.Data, "reads", ordinal: 2);
        var extraKnownIds = new[] { connA.ToFactId(), connB.ToFactId() };

        var forwardNodes = GraphNodeIndex.Build(
            [Component("project", Orders)], [Project(Orders, "Orders")], [], [], [objectA, objectB], []);
        var forward = ComponentGraphProjector.Project(
            [ValidatedWithExtraKnownIds(extraKnownIds, toConnA, toConnB)], forwardNodes);

        var reversedNodes = GraphNodeIndex.Build(
            [Component("project", Orders)], [Project(Orders, "Orders")], [], [], [objectB, objectA], []);
        var reversed = ComponentGraphProjector.Project(
            [ValidatedWithExtraKnownIds(extraKnownIds, toConnB, toConnA)], reversedNodes);

        Assert.Equal(forward.Mermaid, reversed.Mermaid);
        Assert.Equal(2, forward.Edges.Select(static edge => edge.Target.NodeId).Distinct(StringComparer.Ordinal).Count());
    }

    // COMP-18: every Escape replacement rule, each proven by its own case rather than assumed from one.
    [Theory]
    [InlineData("Orders#Co", "Orders#35;Co")]
    [InlineData("Orders\"Co", "Orders#quot;Co")]
    [InlineData("Orders|Co", "Orders#124;Co")]
    [InlineData("Orders\rCo", "OrdersCo")]
    [InlineData("Orders\nCo", "Orders Co")]
    public void Project_NodeLabelContainingASpecialCharacter_EscapesItForMermaid(string rawName, string escapedName)
    {
        var nodes = GraphNodeIndex.Build(
            [Component("project", Orders), Component("project", Payments)],
            [Project(Orders, rawName), Project(Payments, "Payments")],
            [],
            [],
            [],
            []);
        var relation = Relation(Orders.ToFactId(), Payments.ToFactId(), RelationPartition.Structural, "calls");

        var result = ComponentGraphProjector.Project([Validated(relation)], nodes);

        Assert.Contains($"[\"{escapedName}\"]", result.Mermaid, StringComparison.Ordinal);
    }

    // COMP-05: every component the fragments carry appears, ordered by component id, each with its kind
    // and project ids.
    [Fact]
    public void Project_TwoComponents_ListsBothOrderedByComponentIdWithKindAndProjectIds()
    {
        var orders = Component("project", Orders);
        var payments = Component("project", Payments);

        var result = ComponentGraphProjector.Project([Validated(orders, payments)], Nodes());

        var orderedIds = new[] { orders.ComponentId.Value, payments.ComponentId.Value }.Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            result.ComponentIndex.IndexOf(orderedIds[0], StringComparison.Ordinal)
                < result.ComponentIndex.IndexOf(orderedIds[1], StringComparison.Ordinal),
            "Expected the ordinal-first component id to appear before the second in the rendered index.");
        Assert.Contains($"- id: `{orders.ComponentId.Value}`", result.ComponentIndex, StringComparison.Ordinal);
        Assert.Contains($"- project: `{Orders.Value}`", result.ComponentIndex, StringComparison.Ordinal);
        Assert.Contains("## project", result.ComponentIndex, StringComparison.Ordinal);
    }

    // COMP-05: a component touched by no edge in the diagram (Payments here) still appears in the index -
    // the index is driven by the ComponentFacts the fragments carry, not by which nodes an edge happened
    // to touch.
    [Fact]
    public void Project_AComponentWithNoEdgeInTheDiagram_StillAppearsInTheIndex()
    {
        var orders = Component("project", Orders);
        var payments = Component("project", Payments);

        var result = ComponentGraphProjector.Project([Validated(orders, payments)], Nodes());

        Assert.Empty(result.Edges);
        Assert.Contains($"- id: `{payments.ComponentId.Value}`", result.ComponentIndex, StringComparison.Ordinal);
    }

    // COMP-24: a database object is never a ComponentFact, so a fragment that carries one alongside real
    // components still lists nothing about it in the index.
    [Fact]
    public void Project_FragmentsContainingADatabaseObject_NeverListsItInTheComponentIndex()
    {
        var orders = Component("project", Orders);
        var relation = Relation(Orders.ToFactId(), OrdersTable.ToFactId(), RelationPartition.Data, "writes-column");

        var result = ComponentGraphProjector.Project([Validated(orders, DatabaseObject(), relation)], Nodes());

        Assert.DoesNotContain(OrdersTable.Value, result.ComponentIndex, StringComparison.Ordinal);
    }

    // COMP-06 (via the projector): no ComponentFacts at all renders exactly the empty index header.
    [Fact]
    public void Project_NoComponentFacts_RendersExactlyTheComponentsHeaderWithNoEntries()
    {
        var result = ComponentGraphProjector.Project([], Nodes());

        Assert.Equal("# Components\n", result.ComponentIndex);
    }

    private static GraphNodeIndex Nodes() => GraphNodeIndex.Build(
        [Component("project", Orders), Component("project", Payments)],
        [Project(Orders, "Orders"), Project(Payments, "Payments")],
        [],
        [],
        [DatabaseObject()],
        [Column()]);

    private static ValidatedFactFragment Validated(params IFact[] facts) => ValidatedWithExtraKnownIds([], facts);

    private static ValidatedFactFragment ValidatedWithExtraKnownIds(IEnumerable<FactId> extraKnownIds, params IFact[] facts)
    {
        var knownIds = new[] { Orders.ToFactId(), Payments.ToFactId(), Document.ToFactId(), OrdersTable.ToFactId(), OrdersIdColumn.ToFactId() }
            .Concat(extraKnownIds);
        var result = FactValidator.Validate(FactValidationInput.Create(
            facts,
            documents: [DocumentExtent.Create(Document, "Client.cs", [80])],
            knownFactIds: knownIds));
        return Assert.IsType<ValidatedFactFragment>(result.Fragment);
    }

    private static RelationFact Relation(
        FactId sourceId, FactId? targetId, RelationPartition partition, string kind, int ordinal = 1)
    {
        var id = RelationFactId.Create(sourceId, kind, $"target={targetId?.Value ?? "none"}", ordinal);
        var requiresEvidence = kind is not ("project-reference" or "package-reference");
        return new RelationFact(
            FactHeader.Create(
                id.ToFactId(),
                FactKind.Relation,
                targetId is null ? FactResolution.Unresolved : FactResolution.Exact,
                [new FactProvenance("test", "1", requiresEvidence ? Detector : null, requiresEvidence ? "1" : null)],
                requiresEvidence ? [new Evidence(Document, "Client.cs", 1, 1, 1, 2)] : []),
            id,
            sourceId,
            targetId,
            partition,
            kind,
            targetId is null ? "Not proved for the test." : null);
    }

    private static ProjectFact Project(ProjectFactId id, string name) =>
        new(FactHeader.Create(id.ToFactId(), FactKind.Project, FactResolution.Syntactic), id, name, id.Value, [], []);

    private static ComponentFact Component(string kind, params ProjectFactId[] projects)
    {
        var id = ComponentFactId.Create(kind, projects.Select(static project => project.ToFactId()));
        return new ComponentFact(
            FactHeader.Create(id.ToFactId(), FactKind.Component, FactResolution.Syntactic), id, kind, projects.ToImmutableArray());
    }

    private static DatabaseObjectFact DatabaseObject() => new(
        FactHeader.Create(OrdersTable.ToFactId(), FactKind.DatabaseObject, FactResolution.Exact),
        OrdersTable, DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "orders");

    private static DatabaseObjectFact DatabaseObject(DatabaseObjectFactId id, string connection) => new(
        FactHeader.Create(id.ToFactId(), FactKind.DatabaseObject, FactResolution.Exact),
        id, connection, DatabaseObjectKind.Table, "orders");

    private static DatabaseColumnFact Column() => new(
        FactHeader.Create(OrdersIdColumn.ToFactId(), FactKind.DatabaseColumn, FactResolution.Exact),
        OrdersIdColumn, OrdersTable, "id");
}
