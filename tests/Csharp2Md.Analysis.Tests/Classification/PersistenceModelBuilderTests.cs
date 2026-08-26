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

public sealed class PersistenceModelBuilderTests
{
    private const string OrderDbContextFqn = "global::Acme.Orders.Data.OrderDbContext";
    private const string BillingDbContextFqn = "global::Acme.Orders.Data.BillingDbContext";
    private const string OrderFqn = "global::Acme.Orders.Data.Order";
    private const string OrderLineFqn = "global::Acme.Orders.Data.OrderLine";

    [Fact]
    [Trait("Requirement", "PK-10")]
    [Trait("Requirement", "PK-12")]
    public void Build_DbSetPropertyContainer_CreatesOneRelationalStoreNamedAfterTheContextType()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var store = Assert.Single(model.Stores);
        Assert.Equal(OrderDbContextFqn, store.ContextTypeFqn);
        Assert.Equal(DataStoreTechnology.Relational, store.Technology);
        Assert.Equal("Acme.Orders.Data.OrderDbContext", store.Name);
    }

    [Fact]
    [Trait("Requirement", "PK-10")]
    public void Build_ContextTypePayloadWithoutADbSetProperty_StillCreatesTheStore()
    {
        var pipeline = Arrange();
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddDataAccess(pipeline, writer, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var store = Assert.Single(model.Stores);
        Assert.Equal(OrderDbContextFqn, store.ContextTypeFqn);
        Assert.Equal("Acme.Orders.Data.OrderDbContext", store.Name);
    }

    [Fact]
    [Trait("Requirement", "PK-10")]
    public void Build_DbSetPropertyAndContextTypePayloadForOneContext_CreatesOneStore()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "OrderLines", OrderLineFqn);
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddDataAccess(pipeline, writer, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal([OrderDbContextFqn], model.Stores.Select(store => store.ContextTypeFqn));
    }

    [Fact]
    [Trait("Requirement", "PK-11")]
    public void Build_ConfigurationKeyInACallableWhoseSignatureNamesTheContext_NamesTheStoreAfterTheKey()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var registration = AddMethod(pipeline, "AddOrdersDb", "global::Acme.Orders.Program", OrderDbContextFqn);
        AddConfiguration(pipeline, registration, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal("OrdersDb", Assert.Single(model.Stores).Name);
    }

    [Fact]
    [Trait("Requirement", "PK-11")]
    public void Build_ConfigurationKeyInACallableWhosePayloadNamesTheContext_NamesTheStoreAfterTheKey()
    {
        var pipeline = Arrange();
        var registration = AddMethod(pipeline, "ConfigureHost", "global::Acme.Orders.Program");
        AddDataAccess(pipeline, registration, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));
        AddConfiguration(pipeline, registration, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal("OrdersDb", Assert.Single(model.Stores).Name);
    }

    [Fact]
    [Trait("Requirement", "PK-12")]
    public void Build_ConfigurationKeyInACallableThatNeverNamesTheContext_FallsBackToTheTypeName()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var unrelated = AddMethod(pipeline, "ConfigureLogging", "global::Acme.Orders.Program");
        AddConfiguration(pipeline, unrelated, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal("Acme.Orders.Data.OrderDbContext", Assert.Single(model.Stores).Name);
    }

    [Fact]
    [Trait("Requirement", "PK-13")]
    public void Build_LedgerNamingNoContext_CreatesNoStore()
    {
        var pipeline = Arrange();
        var contract = AddMethod(pipeline, "Handle", "global::Acme.Shared.Contracts.OrderPlacedHandler");
        AddConfiguration(pipeline, contract, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Empty(model.Stores);
    }

    [Fact]
    [Trait("Requirement", "PK-10")]
    [Trait("Requirement", "PK-11")]
    [Trait("Requirement", "PK-12")]
    public void Build_TwoContextsInOneSolution_CreatesTwoStoresOrdinalSortedAndNamedIndependently()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        AddDbSetProperty(pipeline, BillingDbContextFqn, "Invoices", "global::Acme.Orders.Data.Invoice");
        var registration = AddMethod(pipeline, "AddOrdersDb", "global::Acme.Orders.Program", OrderDbContextFqn);
        AddConfiguration(pipeline, registration, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal(
            [BillingDbContextFqn, OrderDbContextFqn],
            model.Stores.Select(store => store.ContextTypeFqn));
        Assert.Equal("Acme.Orders.Data.BillingDbContext", model.Stores[0].Name);
        Assert.Equal("OrdersDb", model.Stores[1].Name);
    }

    [Fact]
    [Trait("Requirement", "PK-14")]
    [Trait("Requirement", "PK-17")]
    public void Build_DbSetMember_CreatesOneTableObjectWithAnUnknownSchemaAndTheEntitySymbol()
    {
        var pipeline = Arrange();
        var entity = AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var dataObject = Assert.Single(Assert.Single(model.Stores).Objects);
        Assert.Equal(OrderFqn, dataObject.EntityTypeFqn);
        Assert.Equal(DataObjectForm.Table, dataObject.Form);
        Assert.Equal("unknown", dataObject.SchemaName);
        Assert.Equal(entity.Reference, dataObject.ClrSymbol);
    }

    [Fact]
    [Trait("Requirement", "PK-15")]
    public void Build_ProvenToTable_SetsThePhysicalNameAndExplicitConfirmation()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var configure = AddMethod(pipeline, "Configure", "global::Acme.Orders.Data.OrderConfiguration");
        AddInvocation(pipeline, configure, ordinal: 1, ("entity-type", OrderFqn), ("table-name", "order_headers"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var dataObject = Assert.Single(Assert.Single(model.Stores).Objects);
        Assert.Equal("order_headers", dataObject.TableName);
        Assert.Equal(MappingStateKind.ExplicitConfirmation, dataObject.MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-16")]
    public void Build_NoToTable_FallsBackToTheDbSetMemberNameAsAConventionalCandidate()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderLineFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "OrderLines", OrderLineFqn);

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var dataObject = Assert.Single(Assert.Single(model.Stores).Objects);
        Assert.Equal("OrderLines", dataObject.TableName);
        Assert.Equal(MappingStateKind.ConventionalCandidate, dataObject.MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-16")]
    public void Build_NonConstantToTableArgument_FallsBackToConvention()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var configure = AddMethod(pipeline, "Configure", "global::Acme.Orders.Data.OrderConfiguration");
        AddInvocation(pipeline, configure, ordinal: 1, ("entity-type", OrderFqn), ("method-name", "ToTable"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var dataObject = Assert.Single(Assert.Single(model.Stores).Objects);
        Assert.Equal("Orders", dataObject.TableName);
        Assert.Equal(MappingStateKind.ConventionalCandidate, dataObject.MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-14")]
    [Trait("Requirement", "PK-20")]
    public void Build_TwoDbSetMembersExposingOneEntityType_ProducesTwoUnmergedObjects()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "ArchivedOrders", OrderFqn);

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var objects = Assert.Single(model.Stores).Objects;
        Assert.Equal<string>(["ArchivedOrders", "Orders"], objects.Select(dataObject => dataObject.TableName));
        Assert.All(objects, dataObject => Assert.Equal(OrderFqn, dataObject.EntityTypeFqn));
    }

    [Fact]
    [Trait("Requirement", "PK-18")]
    public void Build_SqlTargetUnderAContext_CreatesOneConventionalCandidateTableObject()
    {
        var pipeline = Arrange();
        var reader = AddMethod(pipeline, "SelectOrder", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            reader,
            ordinal: 1,
            ("operation", "read"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "read"),
            ("sql-target", "Orders"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var dataObject = Assert.Single(Assert.Single(model.Stores).Objects);
        Assert.Null(dataObject.EntityTypeFqn);
        Assert.Equal("Orders", dataObject.TableName);
        Assert.Equal(DataObjectForm.Table, dataObject.Form);
        Assert.Equal(MappingStateKind.ConventionalCandidate, dataObject.MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-18")]
    public void Build_SqlOnADbSetReceiver_PlacesTheObjectUnderTheContextExposingThatEntity()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var reader = AddMethod(pipeline, "SelectOrder", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            reader,
            ordinal: 1,
            ("operation", "read"),
            ("entity-type", OrderFqn),
            ("sql-operation", "read"),
            ("sql-target", "OrderArchive"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var store = Assert.Single(model.Stores);
        Assert.Equal(OrderDbContextFqn, store.ContextTypeFqn);
        Assert.Contains(store.Objects, dataObject => dataObject.TableName == "OrderArchive" && dataObject.EntityTypeFqn is null);
    }

    [Fact]
    [Trait("Requirement", "PK-21")]
    public void Build_ExecStatementTarget_GetsFormUnknown()
    {
        var pipeline = Arrange();
        var caller = AddMethod(pipeline, "RebuildTotals", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            caller,
            ordinal: 1,
            ("operation", "execute"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "execute"),
            ("sql-target", "usp_RebuildOrderTotals"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var dataObject = Assert.Single(Assert.Single(model.Stores).Objects);
        Assert.Equal("usp_RebuildOrderTotals", dataObject.TableName);
        Assert.Equal(DataObjectForm.Unknown, dataObject.Form);
        Assert.Equal(MappingStateKind.ConventionalCandidate, dataObject.MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-18")]
    public void Build_TwoStatementsNamingOneTarget_ShareOneObject()
    {
        var pipeline = Arrange();
        var reader = AddMethod(pipeline, "SelectOrder", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            reader,
            ordinal: 1,
            ("operation", "read"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "read"),
            ("sql-target", "Orders"));
        var deleter = AddMethod(pipeline, "DeleteArchived", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            deleter,
            ordinal: 1,
            ("operation", "delete"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "delete"),
            ("sql-target", "Orders"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var dataObject = Assert.Single(Assert.Single(model.Stores).Objects);
        Assert.Equal("Orders", dataObject.TableName);
        Assert.Equal(DataObjectForm.Table, dataObject.Form);
    }

    [Fact]
    [Trait("Requirement", "PK-20")]
    public void Build_SqlTargetAndMappedEntityObject_StayTwoObjectsDespiteTheirNames()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var configure = AddMethod(pipeline, "Configure", "global::Acme.Orders.Data.OrderConfiguration");
        AddInvocation(pipeline, configure, ordinal: 1, ("entity-type", OrderFqn), ("table-name", "order_headers"));
        var reader = AddMethod(pipeline, "SelectOrder", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            reader,
            ordinal: 1,
            ("operation", "read"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "read"),
            ("sql-target", "Orders"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var objects = Assert.Single(model.Stores).Objects;
        Assert.Equal<string>(["Orders", "order_headers"], objects.Select(dataObject => dataObject.TableName));
        Assert.Equal(MappingStateKind.ConventionalCandidate, objects[0].MappingState);
        Assert.Equal(MappingStateKind.ExplicitConfirmation, objects[1].MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-20")]
    public void Build_SqlTargetDifferingOnlyByPluralizationFromAnEntityObject_IsNotMerged()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Order", OrderFqn);
        var reader = AddMethod(pipeline, "SelectOrder", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            reader,
            ordinal: 1,
            ("operation", "read"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "read"),
            ("sql-target", "Orders"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var objects = Assert.Single(model.Stores).Objects;
        Assert.Equal<string>(["Order", "Orders"], objects.Select(dataObject => dataObject.TableName));
        Assert.Equal(OrderFqn, objects[0].EntityTypeFqn);
        Assert.Null(objects[1].EntityTypeFqn);
    }

    [Fact]
    [Trait("Requirement", "PK-22")]
    [Trait("Requirement", "PK-26")]
    public void Build_FieldNamesOnALinqRead_CreatesAConventionalFieldCarryingThePropertySymbol()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        var status = AddPropertySymbol(pipeline, OrderFqn, "Status");
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        AddDataAccess(pipeline, reader, ordinal: 1, ("operation", "read"), ("entity-type", OrderFqn), ("field-names", "Status"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var field = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Fields);
        Assert.Equal("Status", field.PropertyName);
        Assert.Equal("Status", field.FieldName);
        Assert.Equal(MappingStateKind.ConventionalCandidate, field.MappingState);
        Assert.Equal(status.Reference, field.ClrSymbol);
    }

    [Fact]
    [Trait("Requirement", "PK-22")]
    public void Build_PropertiesReachedInsideAnAnonymousProjection_EachProduceAField()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        AddDataAccess(
            pipeline,
            reader,
            ordinal: 1,
            ("operation", "read"),
            ("entity-type", OrderFqn),
            ("field-names", "Amount|Id|Status"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var fields = Assert.Single(Assert.Single(model.Stores).Objects).Fields;
        Assert.Equal<string>(["Amount", "Id", "Status"], fields.Select(field => field.FieldName));
    }

    [Fact]
    [Trait("Requirement", "PK-23")]
    public void Build_AssignmentPairedWithASaveChangesInTheSameCallable_CreatesAField()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddAssignment(pipeline, writer, ordinal: 1, ("entity-type", OrderFqn), ("field-name", "Status"));
        AddDataAccess(pipeline, writer, ordinal: 2, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var field = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Fields);
        Assert.Equal("Status", field.FieldName);
    }

    [Fact]
    [Trait("Requirement", "PK-23")]
    public void Build_AssignmentWithoutASaveChangesInTheSameCallable_CreatesNoField()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var writer = AddMethod(pipeline, "Mutate", "global::Acme.Orders.Data.OrderWrites");
        AddAssignment(pipeline, writer, ordinal: 1, ("entity-type", OrderFqn), ("field-name", "Status"));
        var other = AddMethod(pipeline, "Flush", "global::Acme.Orders.Data.OrderWrites");
        AddDataAccess(pipeline, other, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Empty(Assert.Single(Assert.Single(model.Stores).Objects).Fields);
    }

    [Fact]
    [Trait("Requirement", "PK-23")]
    [Trait("Requirement", "PK-42")]
    public void Build_AssignmentToATypeNoEntitySetExposes_CreatesNoField()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddAssignment(pipeline, writer, ordinal: 1, ("entity-type", "global::Acme.Orders.Data.AuditEntry"), ("field-name", "Note"));
        AddDataAccess(pipeline, writer, ordinal: 2, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.All(Assert.Single(model.Stores).Objects, dataObject => Assert.Empty(dataObject.Fields));
    }

    [Fact]
    [Trait("Requirement", "PK-24")]
    public void Build_SqlColumnList_CreatesOneFieldPerColumnUnderTheStatementTarget()
    {
        var pipeline = Arrange();
        var writer = AddMethod(pipeline, "InsertOrder", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            writer,
            ordinal: 1,
            ("operation", "insert"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "insert"),
            ("sql-target", "Orders"),
            ("sql-columns", "Amount|Id|Status"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var fields = Assert.Single(Assert.Single(model.Stores).Objects).Fields;
        Assert.Equal<string>(["Amount", "Id", "Status"], fields.Select(field => field.FieldName));
        Assert.All(fields, field => Assert.Null(field.PropertyName));
        Assert.All(fields, field => Assert.Equal(MappingStateKind.ConventionalCandidate, field.MappingState));
    }

    [Fact]
    [Trait("Requirement", "PK-27")]
    public void Build_SelectStarStatement_ContributesNoField()
    {
        var pipeline = Arrange();
        var reader = AddMethod(pipeline, "SelectAll", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            reader,
            ordinal: 1,
            ("operation", "read"),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", "read"),
            ("sql-target", "Orders"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Empty(Assert.Single(Assert.Single(model.Stores).Objects).Fields);
    }

    [Fact]
    [Trait("Requirement", "PK-25")]
    public void Build_ProvenHasColumnName_SetsThePhysicalNameAndExplicitConfirmation()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddPropertySymbol(pipeline, OrderFqn, "Status");
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var configure = AddMethod(pipeline, "Configure", "global::Acme.Orders.Data.OrderConfiguration");
        AddInvocation(
            pipeline,
            configure,
            ordinal: 1,
            ("entity-type", OrderFqn),
            ("property-name", "Status"),
            ("field-name", "order_status"));
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        AddDataAccess(pipeline, reader, ordinal: 1, ("operation", "read"), ("entity-type", OrderFqn), ("field-names", "Status"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var field = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Fields);
        Assert.Equal("Status", field.PropertyName);
        Assert.Equal("order_status", field.FieldName);
        Assert.Equal(MappingStateKind.ExplicitConfirmation, field.MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-26")]
    public void Build_NonConstantHasColumnNameArgument_FallsBackToTheClrPropertyName()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var configure = AddMethod(pipeline, "Configure", "global::Acme.Orders.Data.OrderConfiguration");
        AddInvocation(pipeline, configure, ordinal: 1, ("entity-type", OrderFqn), ("property-name", "Status"));
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        AddDataAccess(pipeline, reader, ordinal: 1, ("operation", "read"), ("entity-type", OrderFqn), ("field-names", "Status"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var field = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Fields);
        Assert.Equal("Status", field.FieldName);
        Assert.Equal(MappingStateKind.ConventionalCandidate, field.MappingState);
    }

    [Theory]
    [Trait("Requirement", "PK-35")]
    [InlineData("read", DataOperationKind.Read)]
    [InlineData("insert", DataOperationKind.Insert)]
    [InlineData("update", DataOperationKind.Update)]
    [InlineData("delete", DataOperationKind.Delete)]
    [InlineData("execute", DataOperationKind.Execute)]
    public void Build_StatementOperation_TakesTheKindTheClosedTableAssigns(string sqlOperation, DataOperationKind expected)
    {
        var pipeline = Arrange();
        var caller = AddMethod(pipeline, "Run", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(
            pipeline,
            caller,
            ordinal: 1,
            ("operation", sqlOperation),
            ("context-type", OrderDbContextFqn),
            ("sql-operation", sqlOperation),
            ("sql-target", "Orders"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var operation = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Operations);
        Assert.Equal(expected, operation.Kind);
        Assert.Equal("csharp2md.classifier.persistence-sql", operation.Classifier.Id);
        Assert.Equal(1, operation.Classifier.Version);
    }

    [Theory]
    [Trait("Requirement", "PK-35")]
    [InlineData("insert", DataOperationKind.Insert)]
    [InlineData("read", DataOperationKind.Read)]
    public void Build_EntitySetOperation_TakesTheKindTheClosedTableAssigns(string operation, DataOperationKind expected)
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var caller = AddMethod(pipeline, "Run", "global::Acme.Orders.Data.OrderWrites");
        AddDataAccess(pipeline, caller, ordinal: 1, ("operation", operation), ("entity-type", OrderFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var resolved = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Operations);
        Assert.Equal(expected, resolved.Kind);
        Assert.Equal("csharp2md.classifier.persistence-ef", resolved.Classifier.Id);
        Assert.Equal(1, resolved.Classifier.Version);
    }

    [Fact]
    [Trait("Requirement", "PK-35")]
    public void OperationKind_ALiteralOutsideTheClosedTable_IsUnknown()
    {
        Assert.Equal(DataOperationKind.Unknown, PersistenceModelBuilder.OperationKind("save"));
        Assert.Equal(DataOperationKind.Unknown, PersistenceModelBuilder.OperationKind("unknown"));
        Assert.Equal(DataOperationKind.Unknown, PersistenceModelBuilder.OperationKind(null));
    }

    [Fact]
    [Trait("Requirement", "PK-31")]
    [Trait("Requirement", "PK-34")]
    public void Build_ResolvedAccess_CreatesOneOperationPerObjectAndKindCarryingTheFieldsItTouched()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        AddDataAccess(pipeline, reader, ordinal: 1, ("operation", "read"), ("entity-type", OrderFqn), ("field-names", "Id|Status"));
        AddDataAccess(pipeline, reader, ordinal: 2, ("operation", "read"), ("entity-type", OrderFqn), ("field-names", "Amount"));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var operation = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Operations);
        Assert.Equal(DataOperationKind.Read, operation.Kind);
        Assert.Equal<string>(["Amount", "Id", "Status"], operation.FieldNames);
    }

    [Fact]
    [Trait("Requirement", "PK-36")]
    public void Build_AssignmentPairedWithASaveChanges_YieldsAnUpdateOperation()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddAssignment(pipeline, writer, ordinal: 1, ("entity-type", OrderFqn), ("field-name", "Status"));
        AddDataAccess(pipeline, writer, ordinal: 2, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var operation = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Operations);
        Assert.Equal(DataOperationKind.Update, operation.Kind);
        Assert.Equal<string>(["Status"], operation.FieldNames);
        Assert.Equal(writer.Reference, Assert.Single(operation.Callables));
    }

    [Fact]
    [Trait("Requirement", "PK-37")]
    public void Build_BareSaveChangesWithNoTrackedAssignmentOrSetOperation_YieldsNoOperation()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var writer = AddMethod(pipeline, "Flush", "global::Acme.Orders.Data.OrderWrites");
        AddDataAccess(pipeline, writer, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.All(Assert.Single(model.Stores).Objects, dataObject => Assert.Empty(dataObject.Operations));
    }

    [Fact]
    [Trait("Requirement", "PK-38")]
    public void Build_TwoCallablesUpdatingOneObject_ShareOneOperationWithTwoCallables()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var payOrder = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddAssignment(pipeline, payOrder, ordinal: 1, ("entity-type", OrderFqn), ("field-name", "Status"));
        AddDataAccess(pipeline, payOrder, ordinal: 2, ("operation", "unknown"), ("context-type", OrderDbContextFqn));
        var reprice = AddMethod(pipeline, "Reprice", "global::Acme.Orders.Data.OrderWrites");
        AddAssignment(pipeline, reprice, ordinal: 1, ("entity-type", OrderFqn), ("field-name", "Amount"));
        AddDataAccess(pipeline, reprice, ordinal: 2, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var operation = Assert.Single(Assert.Single(Assert.Single(model.Stores).Objects).Operations);
        Assert.Equal(DataOperationKind.Update, operation.Kind);
        Assert.Equal(2, operation.Callables.Length);
        Assert.Contains(payOrder.Reference, operation.Callables);
        Assert.Contains(reprice.Reference, operation.Callables);
        Assert.Equal<string>(["Amount", "Status"], operation.FieldNames);
    }

    [Fact]
    [Trait("Requirement", "PK-39")]
    public void Build_StatementTargetThatNeverReachedThePayload_IsUnresolvedForOperatesOnAndMintsNoObject()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var reader = AddMethod(pipeline, "SelectAllFrom", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(pipeline, reader, ordinal: 1, ("operation", "unknown"), ("entity-type", OrderFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var unresolved = Assert.Single(model.Unresolved);
        Assert.Equal(RelationKind.OperatesOn, unresolved.Kind);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.Equal(reader.Reference, unresolved.Source);
        Assert.Equal<string>(["Orders"], Assert.Single(model.Stores).Objects.Select(dataObject => dataObject.TableName));
    }

    [Fact]
    [Trait("Requirement", "PK-40")]
    public void Build_StatementOnTheContextThatTheReaderRefused_IsUnresolvedForAccessesData()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var caller = AddMethod(pipeline, "MergeOrders", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(pipeline, caller, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var unresolved = Assert.Single(model.Unresolved);
        Assert.Equal(RelationKind.AccessesData, unresolved.Kind);
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        Assert.Equal(caller.Reference, unresolved.Source);
    }

    [Fact]
    [Trait("Requirement", "PK-41")]
    public void Build_EntityTypeWithNoSymbolFact_IsUnresolvedForAccessesDataAndMintsNoOperation()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        AddDataAccess(pipeline, reader, ordinal: 1, ("operation", "read"), ("entity-type", OrderFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var unresolved = Assert.Single(model.Unresolved);
        Assert.Equal(RelationKind.AccessesData, unresolved.Kind);
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        Assert.All(Assert.Single(model.Stores).Objects, dataObject => Assert.Empty(dataObject.Operations));
    }

    [Fact]
    [Trait("Requirement", "PK-42")]
    public void Build_PersistenceNamedTypeWithNoDataAccess_ContributesNothing()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        AddNamedType(pipeline, "global::Acme.Orders.Data.OrderRepository");
        AddMethod(pipeline, "GetById", "global::Acme.Orders.Data.OrderRepository");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Empty(model.Unresolved);
        var store = Assert.Single(model.Stores);
        Assert.Equal<string>(["Orders"], store.Objects.Select(dataObject => dataObject.TableName));
        Assert.All(store.Objects, dataObject => Assert.Empty(dataObject.Operations));
        Assert.Equal(0, model.Coverage.RecognizedOccurrences);
    }

    [Fact]
    [Trait("Requirement", "PK-51")]
    [Trait("Requirement", "PK-52")]
    public void Build_MixedLedger_CountsEveryRecognizedOccurrenceAndNamesTheUnresolvedOwners()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        AddDataAccess(pipeline, reader, ordinal: 1, ("operation", "read"), ("entity-type", OrderFqn));
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddAssignment(pipeline, writer, ordinal: 1, ("entity-type", OrderFqn), ("field-name", "Status"));
        AddDataAccess(pipeline, writer, ordinal: 2, ("operation", "unknown"), ("context-type", OrderDbContextFqn));
        var stray = AddMethod(pipeline, "SelectAllFrom", "global::Acme.Orders.Data.OrderSqlQueries");
        AddDataAccess(pipeline, stray, ordinal: 1, ("operation", "unknown"), ("entity-type", OrderFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal(3, model.Coverage.RecognizedOccurrences);
        Assert.Equal(2, model.Coverage.ResolvedOccurrences);
        Assert.Equal<string>([stray.Reference.Id.Value], model.Coverage.UnresolvedOwnerIds);
    }

    [Fact]
    [Trait("Requirement", "PK-53")]
    public void CoverageCounts_ReportsRawCountsOnly_WithNoPercentageAndNoVerdict()
    {
        var members = typeof(CoverageCounts)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal<string>(["RecognizedOccurrences", "ResolvedOccurrences", "UnresolvedOwnerIds"], members);
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(Project.Create(OrdersProject));
        return pipeline;
    }

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static void AddDbSetProperty(PipelineContext pipeline, string container, string metadata, string entityTypeFqn) =>
        pipeline.Accumulator.AddFact(
            Symbol.Create(
                CanonicalSymbolSignature.Create(
                    "property",
                    container,
                    metadata,
                    0,
                    PersistenceModelBuilder.DbSetTypePrefix + entityTypeFqn + ">"),
                OrdersProject,
                SymbolFacetSet.Create([])));

    private static Symbol AddMethod(
        PipelineContext pipeline,
        string metadata,
        string container,
        string? parameterTypeFqn = null)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                container,
                metadata,
                0,
                "global::System.Void",
                parameterTypeFqn is null ? null : [new SymbolParameterSignature(parameterTypeFqn)]),
            OrdersProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Symbol AddNamedType(PipelineContext pipeline, string fullyQualifiedName)
    {
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create(
                "namedtype",
                fullyQualifiedName[..lastDot],
                fullyQualifiedName[(lastDot + 1)..],
                0,
                fullyQualifiedName),
            OrdersProject,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Symbol AddPropertySymbol(PipelineContext pipeline, string containerFqn, string metadata)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("property", containerFqn, metadata, 0, "global::System.String"),
            OrdersProject,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static void AddAssignment(
        PipelineContext pipeline,
        Symbol owner,
        int ordinal,
        params (string Key, string Value)[] entries) =>
        pipeline.Accumulator.AddObservation(Observe(owner, ObservationKind.Assignment, ordinal, entries));

    private static void AddInvocation(
        PipelineContext pipeline,
        Symbol owner,
        int ordinal,
        params (string Key, string Value)[] entries) =>
        pipeline.Accumulator.AddObservation(Observe(owner, ObservationKind.Invocation, ordinal, entries));

    private static void AddDataAccess(
        PipelineContext pipeline,
        Symbol owner,
        int ordinal,
        params (string Key, string Value)[] entries) =>
        pipeline.Accumulator.AddObservation(
            Observe(owner, ObservationKind.DataAccess, ordinal, entries));

    private static void AddConfiguration(PipelineContext pipeline, Symbol owner, string key) =>
        pipeline.Accumulator.AddObservation(
            Observe(owner, ObservationKind.Configuration, 1, [("key", key)]));

    private static Observation Observe(
        Symbol owner,
        ObservationKind kind,
        int ordinal,
        (string Key, string Value)[] entries) =>
        Observation.Create(
            owner.Reference,
            kind,
            NormalizedPayload.Create(
                entries.Select(entry =>
                    new PayloadEntry(entry.Key, StructuralLiteral.Create(LiteralRole.ProtocolName, entry.Value, entry.Key)))),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/Data/OrderDbContext.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
