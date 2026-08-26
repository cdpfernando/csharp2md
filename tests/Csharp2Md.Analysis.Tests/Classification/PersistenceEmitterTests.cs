using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Persistence;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class PersistenceEmitterTests
{
    [Fact]
    [Trait("Requirement", "PK-14")]
    public void Emit_StoreNameLiteral_UsesSchemaNameRegardlessOfDerivation()
    {
        var pipeline = Arrange();
        var model = Model(Store("OrdersDb", Object("order_headers", MappingStateKind.ExplicitConfirmation)));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var store = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataStore>());
        Assert.Equal(LiteralRole.SchemaName, store.Name.Role);
        Assert.Equal("OrdersDb", store.Name.Value);
        Assert.Equal(DataStoreTechnology.Relational, store.Technology);
    }

    [Fact]
    [Trait("Requirement", "PK-14")]
    public void Emit_DbSetObject_CreatesOneTableDataObjectUnderTheStore()
    {
        var pipeline = Arrange();
        var model = Model(Store("OrdersDb", Object("order_headers", MappingStateKind.ExplicitConfirmation)));

        var result = PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var store = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataStore>());
        var dataObject = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        Assert.Equal(store.Reference, dataObject.Store);
        Assert.Equal(DataObjectForm.Table, dataObject.Form);
        Assert.Equal("order_headers", dataObject.TableName.Value);
        Assert.Equal(LiteralRole.SchemaName, dataObject.SchemaName.Role);
        Assert.Equal(LiteralRole.TableName, dataObject.TableName.Role);
        Assert.Equal(MappingStateKind.ExplicitConfirmation, dataObject.MappingState);
        Assert.Equal(2, result.FactCount);
    }

    [Fact]
    [Trait("Requirement", "PK-18")]
    public void Emit_SqlResolvedTarget_CreatesConventionalCandidateObjectUnderTheStore()
    {
        var pipeline = Arrange();
        var model = Model(Store(
            "OrdersDb",
            Object("Orders", MappingStateKind.ConventionalCandidate, entityTypeFqn: null)));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var store = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataStore>());
        var dataObject = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        Assert.Equal(store.Reference, dataObject.Store);
        Assert.Equal("Orders", dataObject.TableName.Value);
        Assert.Equal(MappingStateKind.ConventionalCandidate, dataObject.MappingState);
        Assert.Equal(
            DataObject.Create(
                store.Reference,
                DataObjectForm.Table,
                StructuralLiteral.Create(LiteralRole.SchemaName, "unknown", "schemaName"),
                StructuralLiteral.Create(LiteralRole.TableName, "Orders", "tableName"),
                MappingStateKind.ConventionalCandidate).Reference,
            dataObject.Reference);
    }

    [Fact]
    [Trait("Requirement", "PK-22")]
    public void Emit_LinqFieldNames_CreatesDataFieldsUnderTheEntityObject()
    {
        var pipeline = Arrange();
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                fields: [Field("Status"), Field("Amount")])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var dataObject = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        var fields = pipeline.Accumulator.ToSnapshot().Facts.OfType<DataField>()
            .OrderBy(field => field.FieldName.Value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["Amount", "Status"], fields.Select(field => field.FieldName.Value));
        Assert.All(fields, field =>
        {
            Assert.Equal(dataObject.Reference, field.DataObject);
            Assert.Equal(LiteralRole.FieldName, field.FieldName.Role);
            Assert.Equal(MappingStateKind.ConventionalCandidate, field.MappingState);
        });
    }

    [Fact]
    [Trait("Requirement", "PK-31")]
    public void Emit_ResolvedDataAccess_CreatesDataOperationWithTargetKindAndMappingState()
    {
        var pipeline = Arrange();
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                operations: [Operation(DataOperationKind.Read)])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var dataObject = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataOperation>());
        Assert.Equal(dataObject.Reference, operation.Target);
        Assert.Equal(DataOperationKind.Read, operation.Operation);
        Assert.Equal(MappingStateKind.ExplicitConfirmation, operation.MappingState);
        Assert.Equal(
            DataOperation.Create(dataObject.Reference, DataOperationKind.Read, MappingStateKind.ExplicitConfirmation).Reference,
            operation.Reference);
    }

    [Fact]
    [Trait("Requirement", "PK-14")]
    public void Emit_Walk_SortsFactsByCanonicalIdAtEveryLevel()
    {
        var pipeline = Arrange();
        var model = Model(
            Store("Zed", Object("zeta", MappingStateKind.ConventionalCandidate)),
            Store(
                "Alpha",
                Object(
                    "orders",
                    MappingStateKind.ExplicitConfirmation,
                    fields: [Field("Status"), Field("Amount")],
                    operations: [Operation(DataOperationKind.Update), Operation(DataOperationKind.Read)])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var snapshot = pipeline.Accumulator.ToSnapshot();
        AssertOrderedById(snapshot.Facts.OfType<DataStore>());
        AssertOrderedById(snapshot.Facts.OfType<DataObject>());
        AssertOrderedById(snapshot.Facts.OfType<DataField>());
        AssertOrderedById(snapshot.Facts.OfType<DataOperation>());
    }

    [Fact]
    [Trait("Requirement", "PK-38")]
    public void Emit_TwoStructurallyIdenticalObjects_DeduplicateThroughAddFactWithoutCorruption()
    {
        var pipeline = Arrange();
        var duplicate = Object("orders", MappingStateKind.ConventionalCandidate);
        var model = Model(Store("OrdersDb", duplicate, duplicate));

        var result = PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        Assert.False(pipeline.Accumulator.StructuralCorruption);
        Assert.Null(pipeline.Accumulator.CollidingIdentity);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        Assert.Equal(2, result.FactCount);
    }

    [Fact]
    [Trait("Requirement", "PK-14")]
    [Trait("Requirement", "PK-20")]
    public void Emit_TwoDbSetMembersExposingOneEntityType_ProducesTwoUnmergedObjects()
    {
        var pipeline = Arrange();
        var model = Model(Store(
            "OrdersDb",
            Object("ArchivedOrders", MappingStateKind.ConventionalCandidate, entityTypeFqn: "global::Acme.Orders.Data.Order"),
            Object("Orders", MappingStateKind.ConventionalCandidate, entityTypeFqn: "global::Acme.Orders.Data.Order")));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var names = pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>()
            .Select(dataObject => dataObject.TableName.Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["ArchivedOrders", "Orders"], names);
    }

    [Fact]
    [Trait("Requirement", "PK-27")]
    public void Emit_ObjectWithNoFields_EmitsNoDataFields()
    {
        var pipeline = Arrange();
        var model = Model(Store("OrdersDb", Object("Orders", MappingStateKind.ConventionalCandidate)));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataField>());
    }

    private static void AssertOrderedById<T>(IEnumerable<T> facts)
        where T : IFact
    {
        var ids = facts.Select(fact => fact.Reference.Id.Value).ToArray();
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        return pipeline;
    }

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static PersistenceModel Model(params StoreNode[] stores) =>
        new([.. stores], [], new CoverageCounts(0, 0, []));

    private static StoreNode Store(string name, params ObjectNode[] objects) =>
        new("global::Acme.Orders.Data.OrderDbContext", DataStoreTechnology.Relational, name, [.. objects]);

    private static ObjectNode Object(
        string tableName,
        MappingStateKind mappingState,
        string? entityTypeFqn = "global::Acme.Orders.Data.Order",
        FieldNode[]? fields = null,
        OperationNode[]? operations = null) =>
        new(
            entityTypeFqn,
            DataObjectForm.Table,
            "unknown",
            tableName,
            mappingState,
            ClrSymbol: null,
            [.. fields ?? []],
            [.. operations ?? []],
            Evidence());

    private static FieldNode Field(string name) =>
        new(name, name, MappingStateKind.ConventionalCandidate, ClrSymbol: null, Evidence());

    private static OperationNode Operation(DataOperationKind kind) =>
        new(
            kind,
            ClassifierIdentity.Create("csharp2md.classifier.persistence-ef", 1),
            [],
            [],
            Evidence());

    private static EvidenceChain Evidence() =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(
                Solution.Create(AcmeSolution).Reference,
                ObservationKind.DataAccess,
                NormalizedPayload.Create([]),
                1),
        ]);
}
