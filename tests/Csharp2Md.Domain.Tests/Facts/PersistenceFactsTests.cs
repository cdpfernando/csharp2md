using System.Reflection;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Facts;

public sealed class PersistenceFactsTests
{
    private static FactReference DataStoreReference(string name)
    {
        var store = DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, name, "name"));
        return store.Reference;
    }

    private static StructuralLiteral SchemaName(string value) => StructuralLiteral.Create(LiteralRole.SchemaName, value, "schemaName");

    private static StructuralLiteral TableName(string value) => StructuralLiteral.Create(LiteralRole.TableName, value, "tableName");

    private static StructuralLiteral FieldName(string value) => StructuralLiteral.Create(LiteralRole.FieldName, value, "fieldName");

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void PersistenceFamily_HasExactlyFourTypes()
    {
        var expectedNames = FactTypeTable.All
            .Where(descriptor => descriptor.Family == FactFamily.Persistence)
            .Select(descriptor => descriptor.Name)
            .ToHashSet();

        var actualNames = typeof(IFact).Assembly.GetTypes()
            .Where(type => typeof(IFact).IsAssignableFrom(type) && !type.IsInterface)
            .Select(type => type.Name)
            .Where(expectedNames.Contains)
            .ToHashSet();

        Assert.Equal(4, expectedNames.Count);
        Assert.Equal(expectedNames, actualNames);
    }

    [Fact]
    [Trait("Requirement", "TAX-22")]
    public void DataStore_Create_UnknownTechnology_IsAcceptedAsARegisteredValue()
    {
        var store = DataStore.Create(DataStoreTechnology.Unknown, StructuralLiteral.Create(LiteralRole.ClientName, "legacy", "name"));

        Assert.Equal(DataStoreTechnology.Unknown, store.Technology);
    }

    [Fact]
    [Trait("Requirement", "TAX-22")]
    public void DataStore_Create_UndefinedTechnology_IsRejectedNamingTheAxisAndTheValue()
    {
        var undefined = (DataStoreTechnology)99;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => DataStore.Create(
            undefined, StructuralLiteral.Create(LiteralRole.ClientName, "legacy", "name")));

        Assert.Contains(nameof(DataStoreTechnology), exception.Message, StringComparison.Ordinal);
        Assert.Equal(undefined, exception.ActualValue);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataStore_Create_CaseDifferingNames_ProduceDistinctIdentitiesUnderOrdinalComparison()
    {
        var lower = DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders", "name"));
        var upper = DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "Orders", "name"));

        Assert.NotEqual(lower.Reference, upper.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-23")]
    public void DataObject_Create_UnknownForm_IsAcceptedAsARegisteredValue()
    {
        var dataObject = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Unknown, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);

        Assert.Equal(DataObjectForm.Unknown, dataObject.Form);
    }

    [Fact]
    [Trait("Requirement", "TAX-23")]
    public void DataObject_Create_UndefinedForm_IsRejectedNamingTheAxisAndTheValue()
    {
        var undefined = (DataObjectForm)99;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => DataObject.Create(
            DataStoreReference("orders-db"), undefined, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved));

        Assert.Contains(nameof(DataObjectForm), exception.Message, StringComparison.Ordinal);
        Assert.Equal(undefined, exception.ActualValue);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataObject_Create_SchemaAndTableNames_MustEnterAsStructuralLiterals_NoConnectionStringParameter()
    {
        var factory = typeof(DataObject).GetMethod(nameof(DataObject.Create), BindingFlags.Public | BindingFlags.Static)!;

        Assert.DoesNotContain(factory.GetParameters(), p => p.ParameterType == typeof(string));
        Assert.Equal(typeof(StructuralLiteral), factory.GetParameters().Single(p => p.Name == "schemaName").ParameterType);
        Assert.Equal(typeof(StructuralLiteral), factory.GetParameters().Single(p => p.Name == "tableName").ParameterType);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataObject_Create_WrongSchemaNameRole_IsRejectedNamingSchemaName()
    {
        var exception = Assert.Throws<ArgumentException>(() => DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, TableName("dbo"), TableName("Orders"), MappingStateKind.Unresolved));

        Assert.Equal("schemaName", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataObject_Create_WrongTableNameRole_IsRejectedNamingTableName()
    {
        var exception = Assert.Throws<ArgumentException>(() => DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), SchemaName("Orders"), MappingStateKind.Unresolved));

        Assert.Equal("tableName", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-31")]
    public void DataObject_Create_MappingStateIsOneOfTheThreeClosedStates()
    {
        var confirmed = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.ExplicitConfirmation);
        var candidate = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.ConventionalCandidate);
        var unresolved = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);

        Assert.Equal(MappingStateKind.ExplicitConfirmation, confirmed.MappingState);
        Assert.Equal(MappingStateKind.ConventionalCandidate, candidate.MappingState);
        Assert.Equal(MappingStateKind.Unresolved, unresolved.MappingState);
    }

    [Fact]
    [Trait("Requirement", "TAX-31")]
    public void DataObject_Create_UndefinedMappingState_IsRejectedNamingTheAxisAndTheValue()
    {
        var undefined = (MappingStateKind)99;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), undefined));

        Assert.Contains(nameof(MappingStateKind), exception.Message, StringComparison.Ordinal);
        Assert.Equal(undefined, exception.ActualValue);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataObject_Create_CaseDifferingTableNames_ProduceDistinctIdentitiesUnderOrdinalComparison()
    {
        var lower = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("orders"), MappingStateKind.Unresolved);
        var upper = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);

        Assert.NotEqual(lower.Reference, upper.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataField_Create_FieldNameMustEnterAsAStructuralLiteral_NoConnectionStringParameter()
    {
        var factory = typeof(DataField).GetMethod(nameof(DataField.Create), BindingFlags.Public | BindingFlags.Static)!;

        Assert.DoesNotContain(factory.GetParameters(), p => p.ParameterType == typeof(string));
        Assert.Equal(typeof(StructuralLiteral), factory.GetParameters().Single(p => p.Name == "fieldName").ParameterType);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataField_Create_WrongFieldNameRole_IsRejectedNamingFieldName()
    {
        var dataObject = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);

        var exception = Assert.Throws<ArgumentException>(() => DataField.Create(
            dataObject.Reference, TableName("CustomerId"), MappingStateKind.Unresolved));

        Assert.Equal("fieldName", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void DataField_Create_CaseDifferingFieldNames_ProduceDistinctIdentitiesUnderOrdinalComparison()
    {
        var dataObject = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);

        var lower = DataField.Create(dataObject.Reference, FieldName("customerid"), MappingStateKind.Unresolved);
        var upper = DataField.Create(dataObject.Reference, FieldName("CustomerId"), MappingStateKind.Unresolved);

        Assert.NotEqual(lower.Reference, upper.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-24")]
    public void DataOperation_Create_UnknownOperation_IsAcceptedAsARegisteredValue()
    {
        var dataObject = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);

        var operation = DataOperation.Create(dataObject.Reference, DataOperationKind.Unknown, MappingStateKind.Unresolved);

        Assert.Equal(DataOperationKind.Unknown, operation.Operation);
    }

    [Fact]
    [Trait("Requirement", "TAX-24")]
    public void DataOperation_Create_UndefinedOperation_IsRejectedNamingTheAxisAndTheValue()
    {
        var dataObject = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);
        var undefined = (DataOperationKind)99;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => DataOperation.Create(
            dataObject.Reference, undefined, MappingStateKind.Unresolved));

        Assert.Contains(nameof(DataOperationKind), exception.Message, StringComparison.Ordinal);
        Assert.Equal(undefined, exception.ActualValue);
    }

    [Fact]
    [Trait("Requirement", "TAX-31")]
    public void DataOperation_Create_MappingStateIsOneOfTheThreeClosedStates()
    {
        var dataObject = DataObject.Create(
            DataStoreReference("orders-db"), DataObjectForm.Table, SchemaName("dbo"), TableName("Orders"), MappingStateKind.Unresolved);

        var operation = DataOperation.Create(dataObject.Reference, DataOperationKind.Read, MappingStateKind.ConventionalCandidate);

        Assert.Equal(MappingStateKind.ConventionalCandidate, operation.MappingState);
    }
}
