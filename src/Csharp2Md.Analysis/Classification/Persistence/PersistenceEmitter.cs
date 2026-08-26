using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Classification.Persistence;

internal static class PersistenceEmitter
{
    public static ClassifierPassResult Emit(PersistenceModel model, ClassifierContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var factCount = 0;

        var stores = model.Stores
            .Select(static node => (Node: node, Fact: CreateStore(node)))
            .OrderBy(static pair => pair.Fact.Reference.Id.Value, StringComparer.Ordinal)
            .ToArray();

        foreach (var (storeNode, storeFact) in stores)
        {
            factCount += AddFact(context, storeFact, seen);

            var objects = storeNode.Objects
                .Select(node => (Node: node, Fact: CreateObject(storeFact, node)))
                .OrderBy(static pair => pair.Fact.Reference.Id.Value, StringComparer.Ordinal)
                .ToArray();

            foreach (var (objectNode, objectFact) in objects)
            {
                factCount += AddFact(context, objectFact, seen);

                foreach (var fieldFact in objectNode.Fields
                    .Select(node => CreateField(objectFact, node))
                    .OrderBy(static fact => fact.Reference.Id.Value, StringComparer.Ordinal))
                {
                    factCount += AddFact(context, fieldFact, seen);
                }

                foreach (var operationFact in objectNode.Operations
                    .Select(node => CreateOperation(objectFact, objectNode, node))
                    .OrderBy(static fact => fact.Reference.Id.Value, StringComparer.Ordinal))
                {
                    factCount += AddFact(context, operationFact, seen);
                }
            }
        }

        return new ClassifierPassResult(factCount, 0, 0, 0);
    }

    private static DataStore CreateStore(StoreNode node) =>
        DataStore.Create(
            node.Technology,
            StructuralLiteral.Create(LiteralRole.SchemaName, node.Name, "name"));

    private static DataObject CreateObject(DataStore store, ObjectNode node) =>
        DataObject.Create(
            store.Reference,
            node.Form,
            StructuralLiteral.Create(LiteralRole.SchemaName, node.SchemaName, "schemaName"),
            StructuralLiteral.Create(LiteralRole.TableName, node.TableName, "tableName"),
            node.MappingState);

    private static DataField CreateField(DataObject dataObject, FieldNode node) =>
        DataField.Create(
            dataObject.Reference,
            StructuralLiteral.Create(LiteralRole.FieldName, node.FieldName, "fieldName"),
            node.MappingState);

    private static DataOperation CreateOperation(DataObject dataObject, ObjectNode objectNode, OperationNode node) =>
        DataOperation.Create(dataObject.Reference, node.Kind, objectNode.MappingState);

    private static int AddFact(ClassifierContext context, IFact fact, HashSet<string> seen)
    {
        context.Accumulator.AddFact(fact);
        return seen.Add(fact.Reference.Id.Value) ? 1 : 0;
    }
}
