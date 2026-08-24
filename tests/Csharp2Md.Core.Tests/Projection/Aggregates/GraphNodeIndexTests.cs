using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class GraphNodeIndexTests
{
    private static readonly ProjectFactId OrdersId = ProjectFactId.Create("src/Orders/Orders.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(OrdersId, "Worker.cs");
    private static readonly SymbolFactId SymbolId =
        SymbolFactId.CreateSyntactic(OrdersId, "Worker.cs", "class", "class:Worker");
    private static readonly DatabaseObjectFactId ObjectId =
        DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "orders");
    private static readonly DatabaseColumnFactId ColumnId = DatabaseColumnFactId.Create(ObjectId, "id");

    // Resolution table row: a project id resolves to that project's component node, labelled with the
    // project's own Name rather than its raw fact id.
    [Fact]
    public void TryResolve_ProjectId_ResolvesToItsComponentNode()
    {
        var index = Build();

        Assert.True(index.TryResolve(OrdersId.ToFactId(), out var node));
        Assert.Equal(GraphNodeShape.Component, node!.Shape);
        Assert.Equal("Orders", node.Label);
        Assert.Equal(ComponentFactId.Create("project", [OrdersId.ToFactId()]).Value, node.NodeId);
    }

    // Resolution table row: a document id resolves through DocumentFact.ProjectId to that project's
    // component node.
    [Fact]
    public void TryResolve_DocumentId_ResolvesToItsOwningProjectsComponentNode()
    {
        var index = Build();

        Assert.True(index.TryResolve(DocumentId.ToFactId(), out var node));
        Assert.Equal(GraphNodeShape.Component, node!.Shape);
        Assert.Equal("Orders", node.Label);
    }

    // Resolution table row: a symbol id resolves through SymbolFact.DocumentId -> DocumentFact.ProjectId
    // to that project's component node.
    [Fact]
    public void TryResolve_SymbolId_ResolvesThroughItsDocumentToItsOwningProjectsComponentNode()
    {
        var index = Build();

        Assert.True(index.TryResolve(SymbolId.ToFactId(), out var node));
        Assert.Equal(GraphNodeShape.Component, node!.Shape);
        Assert.Equal("Orders", node.Label);
    }

    // Resolution table row: a database object id resolves to its own database node, labelled with the
    // object's Name.
    [Fact]
    public void TryResolve_DatabaseObjectId_ResolvesToItsOwnDatabaseNode()
    {
        var index = Build();

        Assert.True(index.TryResolve(ObjectId.ToFactId(), out var node));
        Assert.Equal(GraphNodeShape.DatabaseObject, node!.Shape);
        Assert.Equal("orders", node.Label);
        Assert.Equal(ObjectId.Value, node.NodeId);
    }

    // Resolution table row (COMP-21): a database column id rolls up onto its owning object's node, and
    // the column itself never becomes a node of its own - the resolved NodeId is the object's identity,
    // never the column's.
    [Fact]
    public void TryResolve_DatabaseColumnId_ResolvesToItsOwningObjectsNodeAndNeverBecomesItsOwnNode()
    {
        var index = Build();

        Assert.True(index.TryResolve(ColumnId.ToFactId(), out var node));
        Assert.Equal(GraphNodeShape.DatabaseObject, node!.Shape);
        Assert.Equal(ObjectId.Value, node.NodeId);
        Assert.NotEqual(ColumnId.Value, node.NodeId);
    }

    // Resolution table row: anything else (here, a project id the index was never given) resolves to no
    // node at all.
    [Fact]
    public void TryResolve_UnrelatedFactId_ReturnsFalseWithNoNode()
    {
        var index = Build();

        var unrelated = ProjectFactId.Create("src/Nowhere/Nowhere.csproj").ToFactId();

        Assert.False(index.TryResolve(unrelated, out var node));
        Assert.Null(node);
    }

    // COMP-09: a project claimed by two components is a structural invariant violation, not a silent
    // pick-one.
    [Fact]
    public void Build_AProjectClaimedByTwoComponents_ThrowsInvalidOperationException()
    {
        var project = Project(OrdersId, "Orders");
        var first = Component("project", OrdersId);
        var second = Component("service", OrdersId);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GraphNodeIndex.Build([first, second], [project], [], [], [], []));

        Assert.Contains("belongs to more than one component", exception.Message, StringComparison.Ordinal);
    }

    private static GraphNodeIndex Build()
    {
        var project = Project(OrdersId, "Orders");
        var component = Component("project", OrdersId);
        var document = new DocumentFact(
            FactHeader.Create(DocumentId.ToFactId(), FactKind.Document, FactResolution.Syntactic),
            DocumentId, OrdersId, "Worker.cs", [], [SymbolId]);
        var symbol = Symbol();
        var databaseObject = new DatabaseObjectFact(
            FactHeader.Create(ObjectId.ToFactId(), FactKind.DatabaseObject, FactResolution.Exact),
            ObjectId, DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "orders");
        var column = new DatabaseColumnFact(
            FactHeader.Create(ColumnId.ToFactId(), FactKind.DatabaseColumn, FactResolution.Exact),
            ColumnId, ObjectId, "id");

        return GraphNodeIndex.Build([component], [project], [document], [symbol], [databaseObject], [column]);
    }

    private static SymbolFact Symbol() => new(
        FactHeader.Create(SymbolId.ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
        SymbolId, DocumentId, "class", false, [], [], [], null, "Worker", "Worker", null, null, null, "class Worker", 0, []);

    private static ProjectFact Project(ProjectFactId id, string name) =>
        new(FactHeader.Create(id.ToFactId(), FactKind.Project, FactResolution.Syntactic), id, name, id.Value, [], []);

    private static ComponentFact Component(string kind, params ProjectFactId[] projects)
    {
        var id = ComponentFactId.Create(kind, projects.Select(static project => project.ToFactId()));
        return new ComponentFact(
            FactHeader.Create(id.ToFactId(), FactKind.Component, FactResolution.Syntactic), id, kind, projects.ToImmutableArray());
    }
}
