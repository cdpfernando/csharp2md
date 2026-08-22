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

    private static DatabaseColumnFact Column() => new(
        FactHeader.Create(OrdersIdColumn.ToFactId(), FactKind.DatabaseColumn, FactResolution.Exact),
        OrdersIdColumn, OrdersTable, "id");
}
