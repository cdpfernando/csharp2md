using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
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

public sealed class RelationPassTests
{
    [Fact]
    [Trait("Requirement", "EBC-07")]
    public void Execute_EntryPointAndInboundHttpBoundary_CreatesImplementsOperationWithHttpInboundIdentity()
    {
        var pipeline = Arrange();
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        var operation = AddInboundHttp(pipeline, action, component, "orders/{id}");
        pipeline.Accumulator.AddObservation(
            CreateObservation(action.Reference, ObservationKind.RouteDeclaration, 1, RoutePayload("orders/{id}")));
        pipeline.Accumulator.AddObservation(
            CreateObservation(action.Reference, ObservationKind.AttributeUsage, 2));
        var context = new ClassifierContext(pipeline);

        var result = new RelationPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(1, result.RelationCount);
        Assert.Equal(RelationKind.ImplementsOperation, relation.Kind);
        Assert.Equal(action.Reference, relation.Source);
        Assert.Equal(operation.Reference, relation.Target);
        Assert.Equal(BoundaryPass.HttpInboundIdentity, relation.Classifier);
        Assert.NotEmpty(relation.DerivedFrom.DerivedFrom);
        Assert.Contains(relation.DerivedFrom.DerivedFrom, identity => identity.Kind is ObservationKind.RouteDeclaration);
        Assert.Contains(relation.DerivedFrom.DerivedFrom, identity => identity.Kind is ObservationKind.AttributeUsage);
        Assert.Equal(pipeline.AnalysisVariants, relation.AnalysisVariants);
    }

    [Fact]
    [Trait("Requirement", "EBC-14")]
    public void Execute_OutboundHttpBoundary_CreatesImplementsOperationWithHttpOutboundIdentity()
    {
        var pipeline = Arrange();
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var component = AddComponent(pipeline, [method.Reference]);
        var operation = AddOutboundHttp(pipeline, method, component, "PaymentService", "POST", "payments/authorize");
        pipeline.Accumulator.AddObservation(
            CreateObservation(method.Reference, ObservationKind.Invocation, 1, CreateClientPayload("PaymentService")));
        var context = new ClassifierContext(pipeline);

        new RelationPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(RelationKind.ImplementsOperation, relation.Kind);
        Assert.Equal(method.Reference, relation.Source);
        Assert.Equal(operation.Reference, relation.Target);
        Assert.Equal(BoundaryPass.HttpOutboundIdentity, relation.Classifier);
        Assert.NotEmpty(relation.DerivedFrom.DerivedFrom);
    }

    [Fact]
    [Trait("Requirement", "EBC-20")]
    public void Execute_InboundMessagingBoundary_CreatesImplementsOperationWithMessagingIdentity()
    {
        var pipeline = Arrange();
        var method = AddMethod(pipeline, "HandleAsync", "global::Acme.Orders.Events.OrderPlacedEventHandler", OrdersProject);
        var component = AddComponent(pipeline, [method.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(method.Reference, component.Reference));
        var operation = AddMessaging(pipeline, method, component, BoundaryDirection.Inbound, OrderPlacedFqn);
        pipeline.Accumulator.AddObservation(
            CreateObservation(method.Reference, ObservationKind.MessageOperation, 1, MessagePayload("HandleAsync", OrderPlacedFqn)));
        var context = new ClassifierContext(pipeline);

        new RelationPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(RelationKind.ImplementsOperation, relation.Kind);
        Assert.Equal(BoundaryPass.MessagingIdentity, relation.Classifier);
        Assert.Equal(operation.Reference, relation.Target);
    }

    [Fact]
    [Trait("Requirement", "EBC-23")]
    public void Execute_ContractBinding_CreatesUsesContractWithPayloadRoleFacet()
    {
        var pipeline = Arrange();
        var eventType = AddNamedType(pipeline, "OrderPlaced", "global::Acme.Shared.Contracts", SharedProject);
        var publisher = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var component = AddComponent(pipeline, [publisher.Reference]);
        var operation = AddMessaging(pipeline, publisher, component, BoundaryDirection.Outbound, OrderPlacedFqn);
        var contract = Contract.Create(StructuralLiteral.Create(LiteralRole.ProtocolName, OrderPlacedFqn, "protocol-name"));
        pipeline.Accumulator.AddFact(contract);
        pipeline.Accumulator.AddFact(
            ContractBinding.Create(operation.Reference, "request", eventType.Reference, contract.Reference));
        pipeline.Accumulator.AddObservation(
            CreateObservation(publisher.Reference, ObservationKind.MessageOperation, 1, MessagePayload("PublishAsync", OrderPlacedFqn)));
        var context = new ClassifierContext(pipeline);

        var result = new RelationPass().Execute(context, CancellationToken.None);

        Assert.Equal(2, result.RelationCount);
        var uses = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            relation => relation.Kind is RelationKind.UsesContract);
        Assert.Equal(operation.Reference, uses.Source);
        Assert.Equal(contract.Reference, uses.Target);
        Assert.Equal(ContractPass.Identity, uses.Classifier);
        Assert.Contains(uses.Facets.Entries, entry => entry.AxisName == "payload-role" && entry.WireValue == "request");
        Assert.NotEmpty(uses.DerivedFrom.DerivedFrom);
        Assert.Equal(pipeline.AnalysisVariants, uses.AnalysisVariants);
    }

    [Fact]
    [Trait("Requirement", "EBC-34")]
    public void Execute_AnonymousPublishTypeArgument_RecordsUnresolvedUsesContractNoCandidateFound()
    {
        var pipeline = Arrange();
        var publisher = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [publisher.Reference]);
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                publisher.Reference,
                ObservationKind.MessageOperation,
                1,
                MessagePayload("PublishAsync", "global::<>f__AnonymousType0")));
        var context = new ClassifierContext(pipeline);

        var result = new RelationPass().Execute(context, CancellationToken.None);

        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(1, result.UnresolvedCount);
        Assert.Equal(RelationKind.UsesContract, unresolved.Kind);
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        Assert.Equal(publisher.Reference, unresolved.Source);
    }

    [Fact]
    [Trait("Requirement", "EBC-34")]
    public void Execute_NonConstantCreateClient_RecordsUnresolvedTargetsInsufficientEvidence()
    {
        var pipeline = Arrange();
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [method.Reference]);
        pipeline.Accumulator.AddObservation(
            CreateObservation(method.Reference, ObservationKind.Invocation, 1, CreateClientPayload(clientName: null)));
        var context = new ClassifierContext(pipeline);

        var result = new RelationPass().Execute(context, CancellationToken.None);

        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(1, result.UnresolvedCount);
        Assert.Equal(RelationKind.Targets, unresolved.Kind);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.Equal(method.Reference, unresolved.Source);
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(Project.Create(OrdersProject));
        pipeline.Accumulator.AddFact(Project.Create(SharedProject));
        return pipeline;
    }

    private const string OrderPlacedFqn = "global::Acme.Shared.Contracts.OrderPlaced";

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static ProjectId SharedProject =>
        ProjectId.Create(AcmeSolution, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");

    private static Symbol AddNamedType(PipelineContext pipeline, string metadata, string container, ProjectId project)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("namedtype", container, metadata, 0, container + "." + metadata),
            project,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Symbol AddMethod(PipelineContext pipeline, string metadata, string container, ProjectId project)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", container, metadata, 0, "global::System.Void"),
            project,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Component AddComponent(PipelineContext pipeline, IEnumerable<FactReference> owners)
    {
        var component = Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", owners);
        pipeline.Accumulator.AddFact(component);
        return component;
    }

    private static BoundaryOperation AddInboundHttp(PipelineContext pipeline, Symbol callable, Component component, string route)
    {
        var operation = BoundaryOperation.Create(
            callable.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            BoundaryProtocol.Http,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, route, "protocol-operation-key"));
        pipeline.Accumulator.AddFact(operation);
        return operation;
    }

    private static BoundaryOperation AddOutboundHttp(
        PipelineContext pipeline,
        Symbol callable,
        Component component,
        string clientName,
        string httpMethod,
        string route)
    {
        var operation = BoundaryOperation.Create(
            callable.Reference,
            component.Reference,
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            clientName,
            httpMethod,
            StructuralLiteral.Create(LiteralRole.Route, route, BoundaryPass.RouteKey));
        pipeline.Accumulator.AddFact(operation);
        return operation;
    }

    private static BoundaryOperation AddMessaging(
        PipelineContext pipeline,
        Symbol callable,
        Component component,
        BoundaryDirection direction,
        string eventType)
    {
        var operation = BoundaryOperation.Create(
            callable.Reference,
            component.Reference,
            direction,
            BoundaryProtocol.Messaging,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, eventType, "protocol-operation-key"));
        pipeline.Accumulator.AddFact(operation);
        return operation;
    }

    private static NormalizedPayload RoutePayload(string template) =>
        NormalizedPayload.Create(
        [
            new PayloadEntry(
                BoundaryPass.RouteKey,
                StructuralLiteral.Create(LiteralRole.Route, template, BoundaryPass.RouteKey)),
        ]);

    private static NormalizedPayload CreateClientPayload(string? clientName)
    {
        var entries = new List<PayloadEntry>
        {
            new(
                BoundaryPass.MethodNameKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, BoundaryPass.CreateClientMethodName, BoundaryPass.MethodNameKey)),
            new(
                BoundaryPass.TargetTypeKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, BoundaryPass.HttpClientFactoryTypeName, BoundaryPass.TargetTypeKey)),
        };
        if (clientName is not null)
        {
            entries.Add(
                new PayloadEntry(
                    BoundaryPass.ClientNameKey,
                    StructuralLiteral.Create(LiteralRole.ClientName, clientName, BoundaryPass.ClientNameKey)));
        }

        return NormalizedPayload.Create(entries);
    }

    private static NormalizedPayload MessagePayload(string methodName, string eventType) =>
        NormalizedPayload.Create(
        [
            new PayloadEntry(
                BoundaryPass.MethodNameKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, methodName, BoundaryPass.MethodNameKey)),
            new PayloadEntry(
                BoundaryPass.TargetTypeKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, BoundaryPass.EventBusTypeName, BoundaryPass.TargetTypeKey)),
            new PayloadEntry(
                BoundaryPass.TypeArgumentKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, eventType, BoundaryPass.TypeArgumentKey)),
        ]);

    private static Observation CreateObservation(
        FactReference owner,
        ObservationKind kind,
        int ordinal,
        NormalizedPayload? payload = null) =>
        Observation.Create(
            owner,
            kind,
            payload ?? NormalizedPayload.Create([]),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Orders/Api/OrdersController.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
