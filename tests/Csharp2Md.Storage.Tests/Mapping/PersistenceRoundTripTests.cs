using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class PersistenceRoundTripTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Orders/Acme.Orders.csproj");

    [Fact]
    [Trait("Requirement", "PK-46")]
    public void RoundTrip_PersistenceFacts_PreserveIdentitiesAndFacets()
    {
        var store = OrdersStore();
        var dataObject = HeadersObject(store);
        var field = StatusField(dataObject);
        var operation = UpdateOperation(dataObject);
        var snapshot = Snapshot([store, dataObject, field, operation]);

        var restored = DomainMapper.FromWire(DomainMapper.ToWire(snapshot, Context));

        var restoredStore = Assert.Single(restored.Facts.OfType<DataStore>());
        var restoredObject = Assert.Single(restored.Facts.OfType<DataObject>());
        var restoredField = Assert.Single(restored.Facts.OfType<DataField>());
        var restoredOperation = Assert.Single(restored.Facts.OfType<DataOperation>());
        Assert.Equal(store, restoredStore);
        Assert.Equal(dataObject, restoredObject);
        Assert.Equal(field, restoredField);
        Assert.Equal(operation, restoredOperation);
        Assert.Equal(LiteralRole.SchemaName, restoredStore.Name.Role);
        Assert.Equal(LiteralRole.SchemaName, restoredObject.SchemaName.Role);
        Assert.Equal(LiteralRole.TableName, restoredObject.TableName.Role);
        Assert.Equal(LiteralRole.FieldName, restoredField.FieldName.Role);
        Assert.Equal(DataStoreTechnology.Relational, restoredStore.Technology);
        Assert.Equal(MappingStateKind.ExplicitConfirmation, restoredObject.MappingState);
    }

    [Fact]
    [Trait("Requirement", "PK-46")]
    public void RoundTrip_AccessesDataAndOperatesOn_PreserveFacetsAndEvidenceChains()
    {
        var callable = CallableSymbol();
        var store = OrdersStore();
        var dataObject = HeadersObject(store);
        var field = StatusField(dataObject);
        var operation = UpdateOperation(dataObject);
        var evidence = ValidEvidence(callable.Reference, ObservationKind.DataAccess);
        var variants = Variants();

        var accesses = ConfirmedRelation.Create(
            RelationKind.AccessesData,
            callable.Reference,
            operation.Reference,
            EmptyFacets(),
            evidence,
            PersistenceEmitterIdentity(),
            variants,
            EvidenceMethod.Semantic,
            sourceFact: callable,
            targetFact: operation);
        var operatesOnObject = ConfirmedRelation.Create(
            RelationKind.OperatesOn,
            operation.Reference,
            dataObject.Reference,
            EmptyFacets(),
            evidence,
            PersistenceEmitterIdentity(),
            variants,
            EvidenceMethod.Semantic,
            sourceFact: operation,
            targetFact: dataObject);
        var operatesOnField = ConfirmedRelation.Create(
            RelationKind.OperatesOn,
            operation.Reference,
            field.Reference,
            EmptyFacets(),
            evidence,
            PersistenceEmitterIdentity(),
            variants,
            EvidenceMethod.Semantic,
            sourceFact: operation,
            targetFact: field);

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(
                new FactualSnapshot(
                    [callable, store, dataObject, field, operation],
                    [],
                    [accesses, operatesOnObject, operatesOnField],
                    [],
                    [],
                    []),
                Context));

        Assert.Equal(accesses, Assert.Single(restored.ConfirmedRelations, relation => relation.Kind is RelationKind.AccessesData));
        Assert.Equal(
            operatesOnObject,
            Assert.Single(
                restored.ConfirmedRelations,
                relation => relation.Kind is RelationKind.OperatesOn && relation.Target.Equals(dataObject.Reference)));
        Assert.Equal(
            operatesOnField,
            Assert.Single(
                restored.ConfirmedRelations,
                relation => relation.Kind is RelationKind.OperatesOn && relation.Target.Equals(field.Reference)));
        Assert.Equal(evidence, restored.ConfirmedRelations[0].DerivedFrom);
        Assert.Contains(restored.Facts, fact => fact.Equals(callable));
        Assert.Contains(restored.Facts, fact => fact.Equals(operation));
        Assert.Contains(restored.Facts, fact => fact.Equals(dataObject));
        Assert.Contains(restored.Facts, fact => fact.Equals(field));
    }

    [Fact]
    [Trait("Requirement", "PK-46")]
    public void RoundTrip_MapsTo_PreservesMappingRoleFacetsAndEvidenceChain()
    {
        var entity = EntitySymbol();
        var property = PropertySymbol();
        var store = OrdersStore();
        var dataObject = HeadersObject(store);
        var field = StatusField(dataObject);
        var evidence = ValidEvidence(entity.Reference, ObservationKind.Invocation);
        var variants = Variants();

        var mapsObject = ConfirmedRelation.Create(
            RelationKind.MapsTo,
            entity.Reference,
            dataObject.Reference,
            MappingRoleFacets("data-object-mapping"),
            evidence,
            PersistenceEmitterIdentity(),
            variants,
            EvidenceMethod.Configured);
        var mapsField = ConfirmedRelation.Create(
            RelationKind.MapsTo,
            property.Reference,
            field.Reference,
            MappingRoleFacets("data-field-mapping"),
            evidence,
            PersistenceEmitterIdentity(),
            variants,
            EvidenceMethod.Configured);

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(
                new FactualSnapshot(
                    [entity, property, store, dataObject, field],
                    [],
                    [mapsObject, mapsField],
                    [],
                    [],
                    []),
                Context));

        var restoredObject = Assert.Single(
            restored.ConfirmedRelations,
            relation => relation.Target.Equals(dataObject.Reference));
        var restoredField = Assert.Single(
            restored.ConfirmedRelations,
            relation => relation.Target.Equals(field.Reference));
        Assert.Equal(mapsObject, restoredObject);
        Assert.Equal(mapsField, restoredField);
        Assert.Contains(
            restoredObject.Facets.Entries,
            entry => entry.AxisName == "mapping-role" && entry.WireValue == "data-object-mapping");
        Assert.Contains(
            restoredField.Facets.Entries,
            entry => entry.AxisName == "mapping-role" && entry.WireValue == "data-field-mapping");
        Assert.Equal(evidence, restoredObject.DerivedFrom);
        Assert.Equal(evidence, restoredField.DerivedFrom);
    }

    [Fact]
    [Trait("Requirement", "PK-46")]
    public void RoundTrip_PersistenceCandidateAndUnresolved_EqualsOriginal()
    {
        var entity = EntitySymbol();
        var store = OrdersStore();
        var dataObject = HeadersObject(store);
        var evidence = ValidEvidence(entity.Reference, ObservationKind.Invocation);
        var candidate = CandidateLink.Create(RelationKind.MapsTo, entity.Reference, dataObject.Reference, evidence);
        var unresolved = UnresolvedRecord.Create(
            RelationKind.OperatesOn,
            CallableSymbol().Reference,
            UnresolvedCause.InsufficientEvidence,
            evidence);

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(
                new FactualSnapshot([entity, store, dataObject], [], [], [candidate], [unresolved], []),
                Context));

        Assert.Equal(candidate, Assert.Single(restored.Candidates.ToArray()));
        Assert.Equal(RelationKind.MapsTo, restored.Candidates[0].Kind);
        Assert.Equal(dataObject.Reference, restored.Candidates[0].ProposedTarget);
        Assert.Equal(unresolved, Assert.Single(restored.Unresolved.ToArray()));
        Assert.Equal(RelationKind.OperatesOn, restored.Unresolved[0].Kind);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, restored.Unresolved[0].Cause);
        Assert.Equal(evidence, restored.Candidates[0].DerivedFrom);
        Assert.Equal(evidence, restored.Unresolved[0].Available);
    }

    [Fact]
    [Trait("Requirement", "PK-46")]
    public void ToWire_CoverageDiagnostic_ReachesDiagnosticsEnvelope()
    {
        var store = OrdersStore();
        var diagnostic = new DiagnosticRecord(
            "persistence-coverage",
            "Recognized data-access occurrences: 3; resolved to operation and target: 2; unresolved owners: id1:symbol;x",
            store.Reference.Id.Value);
        var snapshot = new FactualSnapshot(
            [store],
            [],
            [],
            [],
            [],
            [],
            [diagnostic]);

        var document = DomainMapper.ToWire(snapshot, Context);
        var record = Assert.Single(document.Diagnostics.Records);

        Assert.Equal(diagnostic.Code, record.Code);
        Assert.Equal(diagnostic.Message, record.Message);
        Assert.Equal(diagnostic.IdentityOrKey, record.IdentityOrKey);
        Assert.Equal(store.Reference.Id.Value, record.IdentityOrKey);
        Assert.DoesNotMatch(@"\b\d+(\.\d+)?\s*%", record.Message);
        Assert.False(Path.IsPathRooted(record.IdentityOrKey));
    }

    private static FactualSnapshot Snapshot(IFact[] facts) =>
        new(facts.ToImmutableArray(), [], [], [], [], []);

    private static DataStore OrdersStore() =>
        DataStore.Create(
            DataStoreTechnology.Relational,
            StructuralLiteral.Create(LiteralRole.SchemaName, "OrdersDb", "name"));

    private static DataObject HeadersObject(DataStore store) =>
        DataObject.Create(
            store.Reference,
            DataObjectForm.Table,
            StructuralLiteral.Create(LiteralRole.SchemaName, "unknown", "schemaName"),
            StructuralLiteral.Create(LiteralRole.TableName, "order_headers", "tableName"),
            MappingStateKind.ExplicitConfirmation);

    private static DataField StatusField(DataObject dataObject) =>
        DataField.Create(
            dataObject.Reference,
            StructuralLiteral.Create(LiteralRole.FieldName, "order_status", "fieldName"),
            MappingStateKind.ExplicitConfirmation);

    private static DataOperation UpdateOperation(DataObject dataObject) =>
        DataOperation.Create(dataObject.Reference, DataOperationKind.Update, MappingStateKind.ExplicitConfirmation);

    private static Symbol CallableSymbol() =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Orders.Data.OrderWrites",
                "PayOrder",
                0,
                "global::System.Void"),
            AcmeProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    private static Symbol EntitySymbol() =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "namedtype",
                "global::Acme.Orders.Data",
                "Order",
                0,
                "global::Acme.Orders.Data.Order"),
            AcmeProject,
            SymbolFacetSet.Create([]));

    private static Symbol PropertySymbol() =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "property",
                "global::Acme.Orders.Data.Order",
                "Status",
                0,
                "global::Acme.Shared.Contracts.OrderStatus"),
            AcmeProject,
            SymbolFacetSet.Create([]));

    private static FacetBinding EmptyFacets() =>
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static FacetBinding MappingRoleFacets(string role) =>
        FacetBinding.Create(
            TaxonomyTables.Default.FacetAxes.Add(
                new FacetAxisDescriptor("mapping-role", TaxonomyTables.Default.MappingRoles)),
            ["mapping-role"],
            [new FacetBindingEntry("mapping-role", role)]);

    private static ClassifierIdentity PersistenceEmitterIdentity() =>
        ClassifierIdentity.Create("csharp2md.classifier.persistence-ef", 1);

    private static ImmutableArray<AnalysisVariantId> Variants() =>
        [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];

    private static EvidenceChain ValidEvidence(FactReference owner, ObservationKind kind) =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(owner, kind, NormalizedPayload.Create([]), 1),
        ]);
}
