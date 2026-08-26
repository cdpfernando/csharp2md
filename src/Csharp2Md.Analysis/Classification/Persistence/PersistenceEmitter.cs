using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification.Persistence;

internal static class PersistenceEmitter
{
    internal static ClassifierIdentity EfIdentity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.persistence-ef", 1);

    internal static ClassifierIdentity SqlIdentity { get; } =
        ClassifierIdentity.Create("csharp2md.classifier.persistence-sql", 1);

    private static readonly FacetBinding EmptyFacets =
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static readonly ImmutableArray<FacetAxisDescriptor> MappingRoleAxes =
        TaxonomyTables.Default.FacetAxes.Add(
            new FacetAxisDescriptor("mapping-role", TaxonomyTables.Default.MappingRoles));

    public static ClassifierPassResult Emit(PersistenceModel model, ClassifierContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var factCount = 0;
        var emitted = new List<EmittedObject>();

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

                var fields = objectNode.Fields
                    .Select(node => (Node: node, Fact: CreateField(objectFact, node)))
                    .OrderBy(static pair => pair.Fact.Reference.Id.Value, StringComparer.Ordinal)
                    .ToArray();
                foreach (var (_, fieldFact) in fields)
                {
                    factCount += AddFact(context, fieldFact, seen);
                }

                var operations = objectNode.Operations
                    .Select(node => (Node: node, Fact: CreateOperation(objectFact, objectNode, node)))
                    .OrderBy(static pair => pair.Fact.Reference.Id.Value, StringComparer.Ordinal)
                    .ToArray();
                foreach (var (_, operationFact) in operations)
                {
                    factCount += AddFact(context, operationFact, seen);
                }

                emitted.Add(new EmittedObject(objectNode, objectFact, fields, operations));
            }
        }

        var relationCount = EmitRelations(context, emitted);
        var candidateCount = EmitCandidates(context, emitted);
        var unresolvedCount = EmitUnresolved(context, model);
        EmitCoverageDiagnostic(context, model, stores);
        return new ClassifierPassResult(factCount, relationCount, candidateCount, unresolvedCount);
    }

    private static int EmitRelations(ClassifierContext context, List<EmittedObject> emitted)
    {
        if (context.AnalysisVariants.IsDefaultOrEmpty)
        {
            return 0;
        }

        var symbolsById = context.FactsByType<Symbol>()
            .ToDictionary(static symbol => symbol.Reference.Id.Value, StringComparer.Ordinal);
        var count = 0;

        foreach (var item in emitted)
        {
            count += EmitMapsTo(
                context,
                item.Node.MappingState,
                item.Node.ClrSymbol,
                item.Node.Evidence,
                item.Fact.Reference,
                "data-object-mapping");

            foreach (var (fieldNode, fieldFact) in item.Fields)
            {
                count += EmitMapsTo(
                    context,
                    fieldNode.MappingState,
                    fieldNode.ClrSymbol,
                    fieldNode.Evidence,
                    fieldFact.Reference,
                    "data-field-mapping");
            }

            var fieldsByName = item.Fields
                .GroupBy(static pair => pair.Node.FieldName, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.First().Fact, StringComparer.Ordinal);

            foreach (var (operationNode, operationFact) in item.Operations)
            {
                foreach (var callable in operationNode.Callables
                    .OrderBy(static reference => reference.Id.Value, StringComparer.Ordinal))
                {
                    if (!symbolsById.TryGetValue(callable.Id.Value, out var source))
                    {
                        continue;
                    }

                    context.Accumulator.AddRelation(
                        ConfirmedRelation.Create(
                            RelationKind.AccessesData,
                            source.Reference,
                            operationFact.Reference,
                            EmptyFacets,
                            operationNode.Evidence,
                            operationNode.Classifier,
                            context.AnalysisVariants,
                            EvidenceMethod.Semantic,
                            sourceFact: source,
                            targetFact: operationFact));
                    count++;
                }

                context.Accumulator.AddRelation(
                    ConfirmedRelation.Create(
                        RelationKind.OperatesOn,
                        operationFact.Reference,
                        item.Fact.Reference,
                        EmptyFacets,
                        operationNode.Evidence,
                        operationNode.Classifier,
                        context.AnalysisVariants,
                        EvidenceMethod.Semantic,
                        sourceFact: operationFact,
                        targetFact: item.Fact));
                count++;

                foreach (var fieldName in operationNode.FieldNames
                    .OrderBy(static name => name, StringComparer.Ordinal))
                {
                    if (!fieldsByName.TryGetValue(fieldName, out var fieldFact))
                    {
                        continue;
                    }

                    context.Accumulator.AddRelation(
                        ConfirmedRelation.Create(
                            RelationKind.OperatesOn,
                            operationFact.Reference,
                            fieldFact.Reference,
                            EmptyFacets,
                            operationNode.Evidence,
                            operationNode.Classifier,
                            context.AnalysisVariants,
                            EvidenceMethod.Semantic,
                            sourceFact: operationFact,
                            targetFact: fieldFact));
                    count++;
                }
            }
        }

        return count;
    }

    private static int EmitMapsTo(
        ClassifierContext context,
        MappingStateKind mappingState,
        FactReference? clrSymbol,
        EvidenceChain evidence,
        FactReference target,
        string mappingRole)
    {
        if (mappingState is not MappingStateKind.ExplicitConfirmation || clrSymbol is null)
        {
            return 0;
        }

        var facets = FacetBinding.Create(
            MappingRoleAxes,
            ["mapping-role"],
            [new FacetBindingEntry("mapping-role", mappingRole)]);
        context.Accumulator.AddRelation(
            ConfirmedRelation.Create(
                RelationKind.MapsTo,
                clrSymbol.Value,
                target,
                facets,
                evidence,
                EfIdentity,
                context.AnalysisVariants,
                EvidenceMethod.Configured));
        return 1;
    }

    private static int EmitCandidates(ClassifierContext context, List<EmittedObject> emitted)
    {
        var count = 0;
        foreach (var item in emitted)
        {
            count += EmitCandidate(context, item.Node.MappingState, item.Node.ClrSymbol, item.Node.Evidence, item.Fact.Reference);
            foreach (var (fieldNode, fieldFact) in item.Fields)
            {
                count += EmitCandidate(
                    context,
                    fieldNode.MappingState,
                    fieldNode.ClrSymbol,
                    fieldNode.Evidence,
                    fieldFact.Reference);
            }
        }

        return count;
    }

    private static int EmitCandidate(
        ClassifierContext context,
        MappingStateKind mappingState,
        FactReference? clrSymbol,
        EvidenceChain evidence,
        FactReference target)
    {
        if (mappingState is not MappingStateKind.ConventionalCandidate || clrSymbol is null)
        {
            return 0;
        }

        context.Accumulator.AddCandidate(
            CandidateLink.Create(RelationKind.MapsTo, clrSymbol.Value, target, evidence));
        return 1;
    }

    private static int EmitUnresolved(ClassifierContext context, PersistenceModel model)
    {
        var count = 0;
        foreach (var node in model.Unresolved
            .OrderBy(static record => record.Source.Id.Value, StringComparer.Ordinal)
            .ThenBy(static record => record.Kind.ToString(), StringComparer.Ordinal)
            .ThenBy(static record => record.Cause.ToString(), StringComparer.Ordinal))
        {
            context.Accumulator.AddUnresolved(
                UnresolvedRecord.Create(node.Kind, node.Source, node.Cause, node.Available));
            count++;
        }

        return count;
    }

    private static void EmitCoverageDiagnostic(
        ClassifierContext context,
        PersistenceModel model,
        (StoreNode Node, DataStore Fact)[] stores)
    {
        var identityOrKey = stores.Length > 0
            ? stores[0].Fact.Reference.Id.Value
            : context.FactsByType<Solution>().SingleOrDefault()?.Reference.Id.Value;
        var owners = string.Join(
            ", ",
            model.Coverage.UnresolvedOwnerIds.OrderBy(static id => id, StringComparer.Ordinal));
        var message =
            $"Recognized data-access occurrences: {model.Coverage.RecognizedOccurrences}; " +
            $"resolved to operation and target: {model.Coverage.ResolvedOccurrences}; " +
            $"unresolved owners: {owners}";
        context.Accumulator.AddDiagnostic(new DiagnosticRecord("persistence-coverage", message, identityOrKey));
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

    private sealed record EmittedObject(
        ObjectNode Node,
        DataObject Fact,
        (FieldNode Node, DataField Fact)[] Fields,
        (OperationNode Node, DataOperation Fact)[] Operations);
}
