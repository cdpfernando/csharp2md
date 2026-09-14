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
using Csharp2Md.Domain.Registry;
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
    [Trait("Requirement", "PK-32")]
    public void Emit_AccessesData_FromCallableToOperation_UsesSemanticEvidenceAndNamedObservation()
    {
        var pipeline = Arrange(withVariants: true);
        var callable = AddCallable(pipeline, "GetOrder");
        var evidence = Evidence(callable.Reference, ObservationKind.DataAccess, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                operations:
                [
                    Operation(
                        DataOperationKind.Read,
                        PersistenceEmitter.EfIdentity,
                        [callable.Reference],
                        [],
                        evidence),
                ])));

        var result = PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataOperation>());
        var relation = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            candidate => candidate.Kind is RelationKind.AccessesData);
        Assert.Equal(RelationKind.AccessesData, relation.Kind);
        Assert.Equal(callable.Reference, relation.Source);
        Assert.Equal(operation.Reference, relation.Target);
        Assert.Equal(EvidenceMethod.Semantic, MinimumEvidence(relation.Kind));
        Assert.Equal(PersistenceEmitter.EfIdentity, relation.Classifier);
        Assert.Equal(evidence, relation.DerivedFrom);
        Assert.NotEmpty(relation.DerivedFrom.DerivedFrom);
        Assert.Equal(ObservationKind.DataAccess, Assert.Single(relation.DerivedFrom.DerivedFrom).Kind);
        Assert.True(result.RelationCount >= 1);
        Assert.Equal(
            ConfirmedRelation.Create(
                RelationKind.AccessesData,
                callable.Reference,
                operation.Reference,
                EmptyFacets(),
                evidence,
                PersistenceEmitter.EfIdentity,
                pipeline.AnalysisVariants,
                EvidenceMethod.Semantic,
                sourceFact: callable,
                targetFact: operation),
            relation);
    }

    [Fact]
    [Trait("Requirement", "PK-33")]
    public void Emit_OperatesOn_ReachesTheTargetDataObject()
    {
        var pipeline = Arrange(withVariants: true);
        var callable = AddCallable(pipeline, "GetOrder");
        var evidence = Evidence(callable.Reference, ObservationKind.DataAccess, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                operations:
                [
                    Operation(DataOperationKind.Read, PersistenceEmitter.EfIdentity, [callable.Reference], [], evidence),
                ])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var dataObject = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataOperation>());
        var relation = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            candidate => candidate.Kind is RelationKind.OperatesOn && candidate.Target.Equals(dataObject.Reference));
        Assert.Equal(RelationKind.OperatesOn, relation.Kind);
        Assert.Equal(operation.Reference, relation.Source);
        Assert.Equal(dataObject.Reference, relation.Target);
        Assert.Equal(EvidenceMethod.Semantic, MinimumEvidence(relation.Kind));
        Assert.Equal(evidence, relation.DerivedFrom);
        Assert.Equal(
            ConfirmedRelation.Create(
                RelationKind.OperatesOn,
                operation.Reference,
                dataObject.Reference,
                EmptyFacets(),
                evidence,
                PersistenceEmitter.EfIdentity,
                pipeline.AnalysisVariants,
                EvidenceMethod.Semantic,
                sourceFact: operation,
                targetFact: dataObject),
            relation);
    }

    [Fact]
    [Trait("Requirement", "PK-34")]
    public void Emit_OperatesOn_ReachesEachTouchedDataField()
    {
        var pipeline = Arrange(withVariants: true);
        var callable = AddCallable(pipeline, "GetOrder");
        var evidence = Evidence(callable.Reference, ObservationKind.DataAccess, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                fields: [Field("Status"), Field("Amount")],
                operations:
                [
                    Operation(
                        DataOperationKind.Read,
                        PersistenceEmitter.EfIdentity,
                        [callable.Reference],
                        ["Status", "Amount"],
                        evidence),
                ])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataOperation>());
        var fieldRelations = pipeline.Accumulator.ToSnapshot().ConfirmedRelations
            .Where(relation => relation.Kind is RelationKind.OperatesOn
                && relation.Source.Equals(operation.Reference)
                && pipeline.Accumulator.ToSnapshot().Facts.OfType<DataField>()
                    .Any(field => field.Reference.Equals(relation.Target)))
            .OrderBy(relation => relation.Target.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var fields = pipeline.Accumulator.ToSnapshot().Facts.OfType<DataField>()
            .OrderBy(field => field.FieldName.Value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, fieldRelations.Length);
        Assert.Equal(["Amount", "Status"], fields.Select(field => field.FieldName.Value));
        Assert.Equal(fields.Select(field => field.Reference), fieldRelations.Select(relation => relation.Target));
        Assert.All(
            fieldRelations,
            relation =>
            {
                Assert.Equal(EvidenceMethod.Semantic, MinimumEvidence(relation.Kind));
                Assert.Equal(evidence, relation.DerivedFrom);
                Assert.Empty(relation.Facets.Entries);
            });
    }

    [Fact]
    [Trait("Requirement", "PK-28")]
    public void Emit_MapsTo_ExplicitObject_UsesConfiguredDataObjectMapping()
    {
        var pipeline = Arrange(withVariants: true);
        var entity = AddNamedType(pipeline, "global::Acme.Orders.Data.Order");
        var evidence = Evidence(entity.Reference, ObservationKind.Invocation, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                clrSymbol: entity.Reference,
                evidence: evidence)));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var dataObject = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        var relation = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            candidate => candidate.Kind is RelationKind.MapsTo);
        Assert.Equal(RelationKind.MapsTo, relation.Kind);
        Assert.Equal(entity.Reference, relation.Source);
        Assert.Equal(dataObject.Reference, relation.Target);
        Assert.Equal(EvidenceMethod.Configured, MinimumEvidence(relation.Kind));
        Assert.Equal("mapping-role", Assert.Single(relation.Facets.Entries).AxisName);
        Assert.Equal("data-object-mapping", Assert.Single(relation.Facets.Entries).WireValue);
        Assert.Equal(evidence, relation.DerivedFrom);
        Assert.Equal(PersistenceEmitter.EfIdentity, relation.Classifier);
        Assert.Equal(
            ConfirmedRelation.Create(
                RelationKind.MapsTo,
                entity.Reference,
                dataObject.Reference,
                MappingRoleFacets("data-object-mapping"),
                evidence,
                PersistenceEmitter.EfIdentity,
                pipeline.AnalysisVariants,
                EvidenceMethod.Configured),
            relation);
    }

    [Fact]
    [Trait("Requirement", "PK-29")]
    public void Emit_MapsTo_ExplicitField_UsesConfiguredDataFieldMapping()
    {
        var pipeline = Arrange(withVariants: true);
        var property = AddProperty(pipeline, "global::Acme.Orders.Data.Order", "Status");
        var evidence = Evidence(property.Reference, ObservationKind.Invocation, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                fields:
                [
                    Field("order_status", MappingStateKind.ExplicitConfirmation, property.Reference, evidence),
                ])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var field = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataField>());
        var relation = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            candidate => candidate.Kind is RelationKind.MapsTo);
        Assert.Equal(RelationKind.MapsTo, relation.Kind);
        Assert.Equal(property.Reference, relation.Source);
        Assert.Equal(field.Reference, relation.Target);
        Assert.Equal(EvidenceMethod.Configured, MinimumEvidence(relation.Kind));
        Assert.Equal("mapping-role", Assert.Single(relation.Facets.Entries).AxisName);
        Assert.Equal("data-field-mapping", Assert.Single(relation.Facets.Entries).WireValue);
        Assert.Equal(evidence, relation.DerivedFrom);
        Assert.Equal(
            ConfirmedRelation.Create(
                RelationKind.MapsTo,
                property.Reference,
                field.Reference,
                MappingRoleFacets("data-field-mapping"),
                evidence,
                PersistenceEmitter.EfIdentity,
                pipeline.AnalysisVariants,
                EvidenceMethod.Configured),
            relation);
    }

    [Fact]
    [Trait("Requirement", "PK-32")]
    public void Emit_AccessesData_SqlDerivedOperation_UsesPersistenceSqlClassifier()
    {
        var pipeline = Arrange(withVariants: true);
        var callable = AddCallable(pipeline, "SelectOrder");
        var evidence = Evidence(callable.Reference, ObservationKind.DataAccess, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "Orders",
                MappingStateKind.ConventionalCandidate,
                entityTypeFqn: null,
                operations:
                [
                    Operation(
                        DataOperationKind.Read,
                        PersistenceEmitter.SqlIdentity,
                        [callable.Reference],
                        [],
                        evidence),
                ])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var relation = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            candidate => candidate.Kind is RelationKind.AccessesData);
        Assert.Equal(PersistenceEmitter.SqlIdentity, relation.Classifier);
        Assert.Equal(EvidenceMethod.Semantic, MinimumEvidence(relation.Kind));
        Assert.Equal(evidence, relation.DerivedFrom);
    }

    [Fact]
    [Trait("Requirement", "PK-30")]
    public void Emit_ConventionalCandidateObject_YieldsMapsToCandidateAndNoConfirmedRelation()
    {
        var pipeline = Arrange(withVariants: true);
        var entity = AddNamedType(pipeline, "global::Acme.Orders.Data.OrderLine");
        var evidence = Evidence(entity.Reference, ObservationKind.Invocation, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "OrderLines",
                MappingStateKind.ConventionalCandidate,
                entityTypeFqn: "global::Acme.Orders.Data.OrderLine",
                clrSymbol: entity.Reference,
                evidence: evidence)));

        var result = PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var dataObject = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataObject>());
        var candidate = Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates);
        Assert.Equal(RelationKind.MapsTo, candidate.Kind);
        Assert.Equal(entity.Reference, candidate.Source);
        Assert.Equal(dataObject.Reference, candidate.ProposedTarget);
        Assert.Equal(evidence, candidate.DerivedFrom);
        Assert.DoesNotContain(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            relation => relation.Kind is RelationKind.MapsTo);
        Assert.Equal(1, result.CandidateCount);
    }

    [Fact]
    [Trait("Requirement", "PK-30")]
    public void Emit_ConventionalCandidateField_YieldsMapsToCandidateAndNoConfirmedRelation()
    {
        var pipeline = Arrange(withVariants: true);
        var property = AddProperty(pipeline, "global::Acme.Orders.Data.Order", "Id");
        var evidence = Evidence(property.Reference, ObservationKind.Invocation, ordinal: 1);
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                fields: [Field("Id", MappingStateKind.ConventionalCandidate, property.Reference, evidence)])));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var field = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataField>());
        var candidate = Assert.Single(
            pipeline.Accumulator.ToSnapshot().Candidates,
            link => link.ProposedTarget.Equals(field.Reference));
        Assert.Equal(RelationKind.MapsTo, candidate.Kind);
        Assert.Equal(property.Reference, candidate.Source);
        Assert.Equal(evidence, candidate.DerivedFrom);
        Assert.DoesNotContain(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            relation => relation.Kind is RelationKind.MapsTo && relation.Target.Equals(field.Reference));
    }

    [Fact]
    [Trait("Requirement", "PK-28")]
    [Trait("Requirement", "PK-30")]
    public void Emit_ExplicitConfirmationObject_DoesNotAlsoEmitAMapsToCandidate()
    {
        var pipeline = Arrange(withVariants: true);
        var entity = AddNamedType(pipeline, "global::Acme.Orders.Data.Order");
        var model = Model(Store(
            "OrdersDb",
            Object(
                "order_headers",
                MappingStateKind.ExplicitConfirmation,
                clrSymbol: entity.Reference)));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        Assert.Contains(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            relation => relation.Kind is RelationKind.MapsTo);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Candidates);
    }

    [Fact]
    [Trait("Requirement", "PK-39")]
    [Trait("Requirement", "PK-40")]
    public void Emit_UnresolvedNodes_BecomeUnresolvedRecordsWithCauseAndEvidence()
    {
        var pipeline = Arrange();
        var interpolated = AddCallable(pipeline, "SelectAllFrom");
        var refused = AddCallable(pipeline, "MergeOrders");
        var interpolatedEvidence = Evidence(interpolated.Reference, ObservationKind.DataAccess, 1);
        var refusedEvidence = Evidence(refused.Reference, ObservationKind.DataAccess, 1);
        var model = new PersistenceModel(
            [Store("OrdersDb")],
            [
                new UnresolvedNode(
                    RelationKind.OperatesOn,
                    interpolated.Reference,
                    UnresolvedCause.InsufficientEvidence,
                    interpolatedEvidence),
                new UnresolvedNode(
                    RelationKind.AccessesData,
                    refused.Reference,
                    UnresolvedCause.NoCandidateFound,
                    refusedEvidence),
            ],
            new CoverageCounts(2, 0, [interpolated.Reference.Id.Value, refused.Reference.Id.Value]));

        var result = PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var unresolved = pipeline.Accumulator.ToSnapshot().Unresolved
            .OrderBy(record => record.Source.Id.Value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, result.UnresolvedCount);
        var operatesOn = Assert.Single(unresolved, record => record.Kind is RelationKind.OperatesOn);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, operatesOn.Cause);
        Assert.Equal(interpolated.Reference, operatesOn.Source);
        Assert.Equal(interpolatedEvidence, operatesOn.Available);
        var accessesData = Assert.Single(unresolved, record => record.Kind is RelationKind.AccessesData);
        Assert.Equal(UnresolvedCause.NoCandidateFound, accessesData.Cause);
        Assert.Equal(refused.Reference, accessesData.Source);
        Assert.Equal(refusedEvidence, accessesData.Available);
    }

    [Fact]
    [Trait("Requirement", "PK-51")]
    [Trait("Requirement", "PK-52")]
    [Trait("Requirement", "PK-53")]
    public void Emit_CoverageDiagnostic_NamesCountsAndUnresolvedOwnersWithoutPercentageOrVerdict()
    {
        var pipeline = Arrange();
        var owner = AddCallable(pipeline, "SelectAllFrom");
        var storeName = "OrdersDb";
        var model = new PersistenceModel(
            [Store(storeName)],
            [
                new UnresolvedNode(
                    RelationKind.OperatesOn,
                    owner.Reference,
                    UnresolvedCause.InsufficientEvidence,
                    Evidence(owner.Reference, ObservationKind.DataAccess, 1)),
            ],
            new CoverageCounts(3, 2, [owner.Reference.Id.Value]));

        PersistenceEmitter.Emit(model, new ClassifierContext(pipeline));

        var store = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<DataStore>());
        var diagnostic = Assert.Single(
            pipeline.Accumulator.ToSnapshot().Diagnostics,
            record => record.Code == "persistence-coverage");
        Assert.Equal(store.Reference.Id.Value, diagnostic.IdentityOrKey);
        Assert.DoesNotContain('\\', store.Reference.Id.Value);
        Assert.DoesNotContain('/', store.Reference.Id.Value);
        Assert.Contains("3", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("2", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(owner.Reference.Id.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b\d+(\.\d+)?\s*%", diagnostic.Message);
        Assert.DoesNotContain("percent", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"(?i)\b(pass|fail|passed|failed|verdict)\b", diagnostic.Message);
        Assert.Contains("Recognized data-access occurrences", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("resolved to operation and target", diagnostic.Message, StringComparison.Ordinal);
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

    private static PipelineContext Arrange(bool withVariants = false)
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(Project.Create(OrdersProject));
        if (withVariants)
        {
            pipeline.AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];
        }

        return pipeline;
    }

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static PersistenceModel Model(params StoreNode[] stores) =>
        new([.. stores], [], new CoverageCounts(0, 0, []));

    private static StoreNode Store(string name, params ObjectNode[] objects) =>
        new("global::Acme.Orders.Data.OrderDbContext", DataStoreTechnology.Relational, name, [.. objects]);

    private static ObjectNode Object(
        string tableName,
        MappingStateKind mappingState,
        string? entityTypeFqn = "global::Acme.Orders.Data.Order",
        FieldNode[]? fields = null,
        OperationNode[]? operations = null,
        FactReference? clrSymbol = null,
        EvidenceChain? evidence = null) =>
        new(
            entityTypeFqn,
            DataObjectForm.Table,
            "unknown",
            tableName,
            mappingState,
            clrSymbol,
            [.. fields ?? []],
            [.. operations ?? []],
            evidence ?? Evidence());

    private static FieldNode Field(
        string name,
        MappingStateKind mappingState = MappingStateKind.ConventionalCandidate,
        FactReference? clrSymbol = null,
        EvidenceChain? evidence = null) =>
        new(name, name, mappingState, clrSymbol, evidence ?? Evidence());

    private static OperationNode Operation(
        DataOperationKind kind,
        ClassifierIdentity? classifier = null,
        FactReference[]? callables = null,
        string[]? fieldNames = null,
        EvidenceChain? evidence = null) =>
        new(
            kind,
            classifier ?? PersistenceEmitter.EfIdentity,
            [.. callables ?? []],
            [.. fieldNames ?? []],
            evidence ?? Evidence());

    private static Symbol AddCallable(PipelineContext pipeline, string metadata)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Orders.Data.OrderQueries",
                metadata,
                0,
                "global::System.Void"),
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

    private static Symbol AddProperty(PipelineContext pipeline, string containerFqn, string metadata)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("property", containerFqn, metadata, 0, "global::System.String"),
            OrdersProject,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static EvidenceChain Evidence() =>
        Evidence(Solution.Create(AcmeSolution).Reference, ObservationKind.DataAccess, 1);

    private static EvidenceChain Evidence(FactReference owner, ObservationKind kind, int ordinal) =>
        EvidenceChain.Create([new ObservationIdentity(owner, kind, NormalizedPayload.Create([]), ordinal)]);

    private static FacetBinding EmptyFacets() =>
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static FacetBinding MappingRoleFacets(string mappingRole) =>
        FacetBinding.Create(
            TaxonomyTables.Default.FacetAxes.Add(
                new FacetAxisDescriptor("mapping-role", TaxonomyTables.Default.MappingRoles)),
            ["mapping-role"],
            [new FacetBindingEntry("mapping-role", mappingRole)]);

    private static EvidenceMethod MinimumEvidence(RelationKind kind) =>
        TaxonomyTables.Default.Relations.Single(descriptor => descriptor.Kind == kind).MinimumEvidenceMethod;
}
