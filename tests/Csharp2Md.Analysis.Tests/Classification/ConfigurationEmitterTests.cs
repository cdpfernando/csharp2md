using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Configuration;
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

public sealed class ConfigurationEmitterTests
{
    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Emit_EmptyModel_ReturnsZeroWithoutThrowing()
    {
        var pipeline = Arrange();
        var model = new ConfigurationModel([], [], [], [], new ConfigurationCoverage(0, 0, 0));

        var result = ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        Assert.Equal(0, result.FactCount);
        Assert.Equal(0, result.RelationCount);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<ConfigurationBinding>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Emit_DeclaredKey_CreatesOneBindingBoundToTheComponent()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var evidence = ConfigurationEvidence(component.Reference, 1, "Services:PaymentService");
        var model = new ConfigurationModel(
            [new DeclaredKey("Services:PaymentService", KeyResolution.Literal, "https://payments.internal.acme.local:8443", component.Reference, evidence)],
            [new ConfiguredEdge(component.Reference, "Services:PaymentService", EvidenceChain.Create([evidence]))],
            [],
            [],
            new ConfigurationCoverage(1, 1, 0));

        var result = ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        var binding = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<ConfigurationBinding>());
        Assert.Equal(component.Reference, binding.BoundFact);
        Assert.Equal(LiteralRole.ConfigurationKey, binding.ConfigurationKey.Role);
        Assert.Equal("Services:PaymentService", binding.ConfigurationKey.Value);
        Assert.Equal(
            ConfigurationBinding.Create(
                component.Reference,
                StructuralLiteral.Create(LiteralRole.ConfigurationKey, "Services:PaymentService", "configurationKey")).Reference,
            binding.Reference);
        Assert.Equal(1, result.FactCount);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Emit_TwoKeysSameComponentAndPath_DeduplicatesThroughAddFactWithoutCorruption()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var first = ConfigurationEvidence(component.Reference, 1, "Services:PaymentService");
        var second = ConfigurationEvidence(component.Reference, 2, "Services:PaymentService");
        var model = new ConfigurationModel(
            [
                new DeclaredKey("Services:PaymentService", KeyResolution.Literal, "https://payments.internal.acme.local:8443", component.Reference, first),
                new DeclaredKey("Services:PaymentService", KeyResolution.Literal, "https://payments.dev.acme.local:8443", component.Reference, second),
            ],
            [
                new ConfiguredEdge(component.Reference, "Services:PaymentService", EvidenceChain.Create([first])),
                new ConfiguredEdge(component.Reference, "Services:PaymentService", EvidenceChain.Create([second])),
            ],
            [],
            [],
            new ConfigurationCoverage(2, 1, 0));

        var result = ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        Assert.False(pipeline.Accumulator.StructuralCorruption);
        Assert.Null(pipeline.Accumulator.CollidingIdentity);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<ConfigurationBinding>());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(2, result.RelationCount);
    }

    [Fact]
    [Trait("Requirement", "CDC-36")]
    [Trait("Requirement", "CDC-40")]
    public void Emit_ComponentEdge_EmitsConfiguredByWithDeclaringObservation()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var evidence = ConfigurationEvidence(component.Reference, 1, "ConnectionStrings:OrdersDb");
        var chain = EvidenceChain.Create([evidence]);
        var model = new ConfigurationModel(
            [new DeclaredKey("ConnectionStrings:OrdersDb", KeyResolution.Literal, Address: null, component.Reference, evidence)],
            [new ConfiguredEdge(component.Reference, "ConnectionStrings:OrdersDb", chain)],
            [],
            [],
            new ConfigurationCoverage(1, 1, 0));

        ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        var binding = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<ConfigurationBinding>());
        var relation = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            candidate => candidate.Kind is RelationKind.ConfiguredBy);
        Assert.Equal(component.Reference, relation.Source);
        Assert.Equal(binding.Reference, relation.Target);
        Assert.Equal(EvidenceMethod.Configured, MinimumConfiguredBy);
        Assert.Equal(chain, relation.DerivedFrom);
        Assert.Contains(evidence, relation.DerivedFrom.DerivedFrom.ToArray());
        Assert.Equal(ConfigurationEmitter.Identity, relation.Classifier);
    }

    [Fact]
    [Trait("Requirement", "CDC-37")]
    [Trait("Requirement", "CDC-38")]
    [Trait("Requirement", "CDC-39")]
    [Trait("Requirement", "CDC-40")]
    public void Emit_SymbolStoreAndBoundaryEdges_EmitConfiguredByNamingTheKey()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var project = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Project>());
        var reader = OwnersOf(pipeline, project)[0];
        var store = DataStore.Create(
            DataStoreTechnology.Relational,
            StructuralLiteral.Create(LiteralRole.ClientName, "OrdersDb", "name"));
        pipeline.Accumulator.AddFact(store);
        var callable = AddCallable(pipeline, project);
        var operation = BoundaryOperation.Create(
            callable.Reference,
            component.Reference,
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            "PaymentService",
            "POST",
            StructuralLiteral.Create(LiteralRole.Route, "payments/authorize", "route"));
        pipeline.Accumulator.AddFact(operation);
        var paymentEvidence = ConfigurationEvidence(component.Reference, 1, "Services:PaymentService");
        var storeEvidence = ConfigurationEvidence(component.Reference, 2, "ConnectionStrings:OrdersDb");
        var symbolRead = ConfigurationEvidence(reader, 1, "Services:PaymentService");
        ConfiguredEdge[] edges =
        [
            new ConfiguredEdge(component.Reference, "ConnectionStrings:OrdersDb", EvidenceChain.Create([storeEvidence])),
            new ConfiguredEdge(component.Reference, "Services:PaymentService", EvidenceChain.Create([paymentEvidence])),
            new ConfiguredEdge(operation.Reference, "Services:PaymentService", EvidenceChain.Create([paymentEvidence])),
            new ConfiguredEdge(store.Reference, "ConnectionStrings:OrdersDb", EvidenceChain.Create([storeEvidence])),
            new ConfiguredEdge(reader, "Services:PaymentService", EvidenceChain.Create([symbolRead, paymentEvidence])),
        ];
        var model = new ConfigurationModel(
            [
                new DeclaredKey("Services:PaymentService", KeyResolution.Literal, "https://payments.internal.acme.local:8443", component.Reference, paymentEvidence),
                new DeclaredKey("ConnectionStrings:OrdersDb", KeyResolution.Literal, Address: null, component.Reference, storeEvidence),
            ],
            [
                .. edges
                    .OrderBy(edge => edge.Source.Id.Value, StringComparer.Ordinal)
                    .ThenBy(edge => edge.KeyPath, StringComparer.Ordinal),
            ],
            [],
            [],
            new ConfigurationCoverage(2, 2, 0));

        var result = ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var configured = snapshot.ConfirmedRelations.Where(relation => relation.Kind is RelationKind.ConfiguredBy).ToArray();
        Assert.Equal(5, configured.Length);
        Assert.Equal(5, result.RelationCount);
        Assert.Equal(EvidenceMethod.Configured, MinimumConfiguredBy);
        var paymentBinding = ConfigurationBinding.Create(
            component.Reference,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, "Services:PaymentService", "configurationKey"));
        var storeBinding = ConfigurationBinding.Create(
            component.Reference,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, "ConnectionStrings:OrdersDb", "configurationKey"));
        Assert.Contains(configured, relation => relation.Source.Equals(component.Reference) && relation.Target.Equals(paymentBinding.Reference));
        Assert.Contains(configured, relation => relation.Source.Equals(reader) && relation.DerivedFrom.DerivedFrom.Contains(paymentEvidence));
        Assert.Contains(configured, relation => relation.Source.Equals(store.Reference) && relation.Target.Equals(storeBinding.Reference));
        Assert.Contains(configured, relation => relation.Source.Equals(operation.Reference) && relation.DerivedFrom.DerivedFrom.Contains(paymentEvidence));
        Assert.Equal(
            configured.Select(relation => relation.Source.Id.Value + "\u001f" + relation.Target.Id.Value).OrderBy(key => key, StringComparer.Ordinal),
            configured.Select(relation => relation.Source.Id.Value + "\u001f" + relation.Target.Id.Value));
    }

    [Fact]
    [Trait("Requirement", "CDC-42")]
    public void Emit_UnboundSymbolRead_EmitsUnresolvedConfiguredByAndNoConfirmedRelation()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var reader = OwnersOf(pipeline, Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Project>()))[0];
        var evidence = ConfigurationEvidence(reader, 1, "FeatureManagement:Missing");
        var model = new ConfigurationModel(
            [],
            [],
            [],
            [new UnboundKeyRead(reader, evidence)],
            new ConfigurationCoverage(0, 0, 1));

        var result = ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var unresolved = Assert.Single(snapshot.Unresolved);
        Assert.Equal(RelationKind.ConfiguredBy, unresolved.Kind);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.Equal(reader, unresolved.Source);
        Assert.Contains(evidence, unresolved.Available.DerivedFrom.ToArray());
        Assert.Equal(1, result.UnresolvedCount);
        Assert.DoesNotContain(snapshot.ConfirmedRelations, relation => relation.Kind is RelationKind.ConfiguredBy);
        Assert.Empty(snapshot.Facts.OfType<ConfigurationBinding>());
    }

    [Fact]
    [Trait("Requirement", "CDC-43")]
    [Trait("Requirement", "CDC-46")]
    [Trait("Requirement", "CDC-47")]
    public void Emit_PromoteDecision_ConfirmsTargetsRemovesCandidateAndDoesNotCreateExternalSystem()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var project = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Project>());
        var (operation, external, csharp) = AddTargetsCandidate(pipeline, project, component, "PaymentService");
        var declaring = ConfigurationEvidence(component.Reference, 1, "Services:PaymentService");
        var evidence = EvidenceChain.Create([csharp.Identity, declaring]);
        var candidate = Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates);
        var externalsBefore = pipeline.Accumulator.ToSnapshot().Facts.OfType<ExternalSystem>().Count();
        var model = new ConfigurationModel(
            [new DeclaredKey("Services:PaymentService", KeyResolution.Literal, "https://payments.internal.acme.local:8443", component.Reference, declaring)],
            [],
            [new TargetDecision(candidate, TargetOutcome.Promote, evidence)],
            [],
            new ConfigurationCoverage(1, 0, 0));

        var result = ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var confirmed = Assert.Single(snapshot.ConfirmedRelations, relation => relation.Kind is RelationKind.Targets);
        Assert.Equal(operation.Reference, confirmed.Source);
        Assert.Equal(external.Reference, confirmed.Target);
        Assert.Equal(evidence, confirmed.DerivedFrom);
        Assert.Contains(csharp.Identity, confirmed.DerivedFrom.DerivedFrom.ToArray());
        Assert.Contains(declaring, confirmed.DerivedFrom.DerivedFrom.ToArray());
        Assert.Equal(EvidenceMethod.Configured, MinimumTargets);
        Assert.DoesNotContain(snapshot.Candidates, link => link.Equals(candidate));
        Assert.Equal(externalsBefore, snapshot.Facts.OfType<ExternalSystem>().Count());
        Assert.Equal(1, result.RelationCount);
    }

    [Fact]
    [Trait("Requirement", "CDC-44")]
    public void Emit_FrontierDecision_KeepsCandidateAndOpensFrontierOnOriginatingOccurrence()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var project = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Project>());
        var (operation, _, csharp) = AddTargetsCandidate(pipeline, project, component, "NotificationService");
        var declaring = ConfigurationEvidence(component.Reference, 1, "Services:NotificationService");
        var candidate = Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates);
        var model = new ConfigurationModel(
            [new DeclaredKey("Services:NotificationService", KeyResolution.Dynamic, Address: null, component.Reference, declaring)],
            [],
            [new TargetDecision(candidate, TargetOutcome.Frontier, EvidenceChain.Create([csharp.Identity, declaring]))],
            [],
            new ConfigurationCoverage(1, 0, 0));

        ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        var snapshot = pipeline.Accumulator.ToSnapshot();
        Assert.Equal(candidate, Assert.Single(snapshot.Candidates));
        Assert.DoesNotContain(snapshot.ConfirmedRelations, relation => relation.Kind is RelationKind.Targets);
        var frontier = Assert.Single(snapshot.Frontiers);
        Assert.Equal(csharp.Identity, frontier.Occurrence);
        Assert.Equal(FrontierCause.FurtherContinuationObserved, frontier.Cause);
        Assert.Equal(operation.Reference, candidate.Source);
    }

    [Fact]
    [Trait("Requirement", "CDC-45")]
    public void Emit_LeaveDecision_ChangesNothing()
    {
        var pipeline = Arrange();
        var component = AddComponent(pipeline);
        var project = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Project>());
        AddTargetsCandidate(pipeline, project, component, "ShippingService");
        var candidate = Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates);
        var frontiersBefore = pipeline.Accumulator.ToSnapshot().Frontiers.Length;
        var relationsBefore = pipeline.Accumulator.ToSnapshot().ConfirmedRelations.Length;
        var model = new ConfigurationModel(
            [],
            [],
            [new TargetDecision(candidate, TargetOutcome.Leave, candidate.DerivedFrom)],
            [],
            new ConfigurationCoverage(0, 0, 0));

        ConfigurationEmitter.Emit(model, new ClassifierContext(pipeline));

        var snapshot = pipeline.Accumulator.ToSnapshot();
        Assert.Equal(candidate, Assert.Single(snapshot.Candidates));
        Assert.Equal(relationsBefore, snapshot.ConfirmedRelations.Length);
        Assert.Equal(frontiersBefore, snapshot.Frontiers.Length);
        Assert.DoesNotContain(snapshot.ConfirmedRelations, relation => relation.Kind is RelationKind.Targets);
    }

    private static EvidenceMethod MinimumConfiguredBy =>
        Csharp2Md.Domain.Registry.TaxonomyTables.Default.Relations
            .Single(relation => relation.Kind == RelationKind.ConfiguredBy)
            .MinimumEvidenceMethod;

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln")
        {
            AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")],
        };
        pipeline.Accumulator.AddFact(Solution.Create(SolutionId));
        return pipeline;
    }

    private static SolutionId SolutionId =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static Component AddComponent(PipelineContext pipeline)
    {
        var project = Project.Create(ProjectId.Create(SolutionId, "Acme.Orders/Acme.Orders.csproj"));
        pipeline.Accumulator.AddFact(project);
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.Program", "ConfigureHost", 1, "global::Microsoft.AspNetCore.Builder.WebApplicationBuilder"),
            project.Id,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        var component = Component.Create(SolutionId, "Acme.Orders/Acme.Orders.csproj", [symbol.Reference]);
        pipeline.Accumulator.AddFact(component);
        return component;
    }

    private static Symbol AddCallable(PipelineContext pipeline, Project project)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.OrderService", "PlaceOrderAsync", 3, "global::System.Threading.Tasks.Task"),
            project.Id,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static FactReference[] OwnersOf(PipelineContext pipeline, Project project) =>
        pipeline.Accumulator.ToSnapshot().Facts.OfType<Symbol>()
            .Where(symbol => symbol.OwningProject.Equals(project.Id))
            .Select(symbol => symbol.Reference)
            .ToArray();

    private static EvidenceMethod MinimumTargets =>
        Csharp2Md.Domain.Registry.TaxonomyTables.Default.Relations
            .Single(relation => relation.Kind == RelationKind.Targets)
            .MinimumEvidenceMethod;

    private static (BoundaryOperation Operation, ExternalSystem External, Observation Csharp) AddTargetsCandidate(
        PipelineContext pipeline,
        Project project,
        Component component,
        string clientName)
    {
        var callable = AddCallable(pipeline, project);
        var operation = BoundaryOperation.Create(
            callable.Reference,
            component.Reference,
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            clientName,
            "POST",
            StructuralLiteral.Create(LiteralRole.Route, "payments/authorize", "route"));
        pipeline.Accumulator.AddFact(operation);
        var external = ExternalSystem.Create(
            SolutionId,
            StructuralLiteral.Create(LiteralRole.ClientName, clientName, "client-name"));
        pipeline.Accumulator.AddFact(external);
        var csharp = Observation.Create(
            callable.Reference,
            ObservationKind.Invocation,
            NormalizedPayload.Create(
            [
                new PayloadEntry(
                    "client-name",
                    StructuralLiteral.Create(LiteralRole.ClientName, clientName, "client-name")),
            ]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/OrderService.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
        pipeline.Accumulator.AddObservation(csharp);
        pipeline.Accumulator.AddCandidate(
            CandidateLink.Create(
                RelationKind.Targets,
                operation.Reference,
                external.Reference,
                EvidenceChain.Create([csharp.Identity])));
        return (operation, external, csharp);
    }

    private static ObservationIdentity ConfigurationEvidence(FactReference owner, int ordinal, string key) =>
        new(
            owner,
            ObservationKind.Configuration,
            NormalizedPayload.Create(
            [
                new PayloadEntry(
                    "key",
                    StructuralLiteral.Create(LiteralRole.ConfigurationKey, key, "key")),
            ]),
            ordinal);
}
