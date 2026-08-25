using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Facts;

public sealed record DataStore : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Persistence;

    public DataStoreTechnology Technology { get; }

    public StructuralLiteral Name { get; }

    private DataStore(FactReference reference, DataStoreTechnology technology, StructuralLiteral name)
    {
        Reference = reference;
        Technology = technology;
        Name = name;
    }

    public static DataStore Create(DataStoreTechnology technology, StructuralLiteral name)
    {
        var wireTechnology = FacetAxes.WireValue(technology);
        FactGuards.RequireInitialized(name, nameof(name));

        var id = FactIdGrammar.Create("data-store", ("technology", wireTechnology), ("name", name.Value));
        var reference = new FactReference(id, nameof(DataStore));
        return new DataStore(reference, technology, name);
    }
}

public sealed record DataObject : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Persistence;

    public FactReference Store { get; }

    public DataObjectForm Form { get; }

    public StructuralLiteral SchemaName { get; }

    public StructuralLiteral TableName { get; }

    public MappingStateKind MappingState { get; }

    private DataObject(
        FactReference reference,
        FactReference store,
        DataObjectForm form,
        StructuralLiteral schemaName,
        StructuralLiteral tableName,
        MappingStateKind mappingState)
    {
        Reference = reference;
        Store = store;
        Form = form;
        SchemaName = schemaName;
        TableName = tableName;
        MappingState = mappingState;
    }

    public static DataObject Create(
        FactReference store,
        DataObjectForm form,
        StructuralLiteral schemaName,
        StructuralLiteral tableName,
        MappingStateKind mappingState)
    {
        FactGuards.RequireInitialized(store, nameof(store));
        var wireForm = FacetAxes.WireValue(form);
        RequireLiteralRole(schemaName, LiteralRole.SchemaName, nameof(schemaName));
        RequireLiteralRole(tableName, LiteralRole.TableName, nameof(tableName));
        var wireMappingState = FacetAxes.WireValue(mappingState);

        var id = FactIdGrammar.Create(
            "data-object",
            ("data-store", store.Id.Value),
            ("form", wireForm),
            ("schema", schemaName.Value),
            ("name", tableName.Value),
            ("mapping-state", wireMappingState));
        var reference = new FactReference(id, nameof(DataObject));
        return new DataObject(reference, store, form, schemaName, tableName, mappingState);
    }

    private static void RequireLiteralRole(StructuralLiteral literal, LiteralRole expectedRole, string parameterName)
    {
        FactGuards.RequireInitialized(literal, parameterName);
        if (literal.Role != expectedRole)
        {
            throw new ArgumentException(
                $"'{parameterName}' must be a structural literal with role '{expectedRole}', but was '{literal.Role}'.", parameterName);
        }
    }
}

public sealed record DataField : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Persistence;

    public FactReference DataObject { get; }

    public StructuralLiteral FieldName { get; }

    public MappingStateKind MappingState { get; }

    private DataField(FactReference reference, FactReference dataObject, StructuralLiteral fieldName, MappingStateKind mappingState)
    {
        Reference = reference;
        DataObject = dataObject;
        FieldName = fieldName;
        MappingState = mappingState;
    }

    public static DataField Create(FactReference dataObject, StructuralLiteral fieldName, MappingStateKind mappingState)
    {
        FactGuards.RequireInitialized(dataObject, nameof(dataObject));
        FactGuards.RequireInitialized(fieldName, nameof(fieldName));
        if (fieldName.Role != LiteralRole.FieldName)
        {
            throw new ArgumentException(
                $"'{nameof(fieldName)}' must be a structural literal with role '{nameof(LiteralRole.FieldName)}', but was '{fieldName.Role}'.",
                nameof(fieldName));
        }

        var wireMappingState = FacetAxes.WireValue(mappingState);

        var id = FactIdGrammar.Create(
            "data-field", ("data-object", dataObject.Id.Value), ("name", fieldName.Value), ("mapping-state", wireMappingState));
        var reference = new FactReference(id, nameof(DataField));
        return new DataField(reference, dataObject, fieldName, mappingState);
    }
}

public sealed record DataOperation : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Persistence;

    public FactReference Target { get; }

    public DataOperationKind Operation { get; }

    public MappingStateKind MappingState { get; }

    private DataOperation(FactReference reference, FactReference target, DataOperationKind operation, MappingStateKind mappingState)
    {
        Reference = reference;
        Target = target;
        Operation = operation;
        MappingState = mappingState;
    }

    public static DataOperation Create(FactReference target, DataOperationKind operation, MappingStateKind mappingState)
    {
        FactGuards.RequireInitialized(target, nameof(target));
        var wireOperation = FacetAxes.WireValue(operation);
        var wireMappingState = FacetAxes.WireValue(mappingState);

        var id = FactIdGrammar.Create(
            "data-operation", ("target", target.Id.Value), ("operation", wireOperation), ("mapping-state", wireMappingState));
        var reference = new FactReference(id, nameof(DataOperation));
        return new DataOperation(reference, target, operation, mappingState);
    }
}
