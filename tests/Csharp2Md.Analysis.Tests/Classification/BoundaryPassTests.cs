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

public sealed class BoundaryPassTests
{
    [Fact]
    [Trait("Requirement", "EBC-06")]
    public void Execute_RouteDeclarationOnControllerEntryPoint_CreatesInboundHttpBoundaryWithRouteTemplateKey()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                action.Reference,
                ObservationKind.RouteDeclaration,
                ordinal: 1,
                RoutePayload("orders/{id}")));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(BoundaryDirection.Inbound, operation.Direction);
        Assert.Equal(BoundaryProtocol.Http, operation.Protocol);
        Assert.Equal(action.Reference, operation.Symbol);
        Assert.Equal(component.Reference, operation.OwningComponent);
        Assert.NotNull(operation.ProtocolOperationKey);
        Assert.Equal(LiteralRole.ProtocolName, operation.ProtocolOperationKey.Value.Role);
        Assert.Equal("orders/{id}", operation.ProtocolOperationKey.Value.Value);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "EBC-33")]
    public void Execute_RouteTemplateWithDynamicSegments_UsesTemplateAsIs()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                action.Reference,
                ObservationKind.RouteDeclaration,
                ordinal: 1,
                RoutePayload("orders/{id}")));
        var context = new ClassifierContext(pipeline);

        new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal("orders/{id}", operation.ProtocolOperationKey!.Value.Value);
        Assert.Contains("{id}", operation.ProtocolOperationKey.Value.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("resolved", operation.ProtocolOperationKey.Value.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "EBC-06")]
    public void Execute_ControllerEntryPointWithoutRouteDeclaration_DoesNotCreateBoundaryOperation()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "EBC-06")]
    public void Execute_RouteDeclarationWithEmptyTemplate_DoesNotCreateBoundaryAndRecordsDiagnostic()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(
            CreateObservation(action.Reference, ObservationKind.RouteDeclaration, ordinal: 1));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
        var diagnostic = Assert.Single(pipeline.Accumulator.ToSnapshot().Diagnostics.ToArray());
        Assert.Equal("missing-route-template", diagnostic.Code);
        Assert.Contains(action.Reference.Id.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(action.Reference.Id.Value, diagnostic.IdentityOrKey);
    }

    [Fact]
    [Trait("Requirement", "EBC-06")]
    public void Execute_HandlerEntryPointWithoutRouteDeclaration_DoesNotCreateHttpBoundary()
    {
        var pipeline = ArrangeOrders();
        var handler = AddNamedType(pipeline, "OrderPlacedEventHandler", "global::Acme.Orders.Events", OrdersProject);
        var method = AddMethod(pipeline, "HandleAsync", "global::Acme.Orders.Events.OrderPlacedEventHandler", OrdersProject);
        var component = AddComponent(pipeline, [handler.Reference, method.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(method.Reference, component.Reference));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
    }

    [Fact]
    [Trait("Requirement", "EBC-06")]
    public void HttpInboundIdentity_IsHttpInboundClassifierVersion1()
    {
        Assert.Equal("csharp2md.classifier.http-inbound", BoundaryPass.HttpInboundIdentity.Id);
        Assert.Equal(1, BoundaryPass.HttpInboundIdentity.Version);
    }

    [Fact]
    [Trait("Requirement", "EBC-10")]
    [Trait("Requirement", "EBC-11")]
    [Trait("Requirement", "EBC-12")]
    [Trait("Requirement", "EBC-15")]
    [Trait("Requirement", "EBC-16")]
    public void Execute_PlaceOrderAsyncCreateClientAndPost_CreatesOutboundHttpBoundaryAndCandidateExternalSystem()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        AddCreateClient(pipeline, method.Reference, "PaymentService", ordinal: 1);
        AddHttpInvocation(pipeline, method.Reference, "PostAsJsonAsync", "payments/authorize", ordinal: 2);
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(BoundaryDirection.Outbound, operation.Direction);
        Assert.Equal(BoundaryProtocol.Http, operation.Protocol);
        Assert.Equal("PaymentService", operation.DestinationScope);
        Assert.Equal("POST", operation.HttpMethod);
        Assert.NotNull(operation.Route);
        Assert.Equal(LiteralRole.Route, operation.Route.Value.Role);
        Assert.Equal("payments/authorize", operation.Route.Value.Value);
        Assert.Equal(method.Reference, operation.Symbol);
        var external = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<ExternalSystem>().ToArray());
        Assert.Equal(LiteralRole.ClientName, external.Name.Role);
        Assert.Equal("PaymentService", external.Name.Value);
        var link = Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates.ToArray());
        Assert.Equal(RelationKind.Targets, link.Kind);
        Assert.Equal(operation.Reference, link.Source);
        Assert.Equal(external.Reference, link.ProposedTarget);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Equal(2, result.FactCount);
        Assert.Equal(1, result.CandidateCount);
    }

    [Fact]
    [Trait("Requirement", "EBC-10")]
    [Trait("Requirement", "EBC-11")]
    public void Execute_NotifyOrderPlacedAsync_CreatesOutboundHttpBoundaryForNotificationService()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "NotifyOrderPlacedAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        AddCreateClient(pipeline, method.Reference, "NotificationService", ordinal: 1);
        AddHttpInvocation(pipeline, method.Reference, "PostAsJsonAsync", "notifications/order-placed", ordinal: 2);
        var context = new ClassifierContext(pipeline);

        new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal("NotificationService", operation.DestinationScope);
        Assert.Equal("POST", operation.HttpMethod);
        Assert.Equal("notifications/order-placed", operation.Route!.Value.Value);
    }

    [Fact]
    [Trait("Requirement", "EBC-10")]
    [Trait("Requirement", "EBC-11")]
    public void Execute_RequestShippingAsync_CreatesOutboundHttpBoundaryForShippingService()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "RequestShippingAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        AddCreateClient(pipeline, method.Reference, "ShippingService", ordinal: 1);
        AddHttpInvocation(pipeline, method.Reference, "PostAsJsonAsync", "shipments", ordinal: 2);
        var context = new ClassifierContext(pipeline);

        new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal("ShippingService", operation.DestinationScope);
        Assert.Equal("POST", operation.HttpMethod);
        Assert.Equal("shipments", operation.Route!.Value.Value);
        var external = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<ExternalSystem>().ToArray());
        Assert.Equal("ShippingService", external.Name.Value);
        Assert.Equal(LiteralRole.ClientName, external.Name.Role);
    }

    [Fact]
    [Trait("Requirement", "EBC-13")]
    public void Execute_MultipleOutboundHttpToDifferentClients_CreatesOneBoundaryPerDistinctTuple()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        AddCreateClient(pipeline, method.Reference, "PaymentService", ordinal: 1);
        AddHttpInvocation(pipeline, method.Reference, "PostAsJsonAsync", "payments/authorize", ordinal: 2);
        AddCreateClient(pipeline, method.Reference, "NotificationService", ordinal: 3);
        AddHttpInvocation(pipeline, method.Reference, "PostAsJsonAsync", "notifications/order-placed", ordinal: 4);
        var context = new ClassifierContext(pipeline);

        new BoundaryPass().Execute(context, CancellationToken.None);

        var operations = pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray();
        Assert.Equal(2, operations.Length);
        Assert.Contains(operations, operation => operation.DestinationScope == "PaymentService" && operation.Route!.Value.Value == "payments/authorize");
        Assert.Contains(operations, operation => operation.DestinationScope == "NotificationService" && operation.Route!.Value.Value == "notifications/order-placed");
        Assert.Equal(2, pipeline.Accumulator.ToSnapshot().Candidates.Length);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
    }

    [Fact]
    [Trait("Requirement", "EBC-34")]
    public void Execute_NonConstantCreateClient_RecordsUnresolvedInsufficientEvidence()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                method.Reference,
                ObservationKind.Invocation,
                ordinal: 1,
                CreateClientPayload(clientName: null)));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.Equal(RelationKind.Targets, unresolved.Kind);
        Assert.Equal(method.Reference, unresolved.Source);
        Assert.Equal(1, result.UnresolvedCount);
    }

    [Fact]
    [Trait("Requirement", "EBC-14")]
    public void HttpOutboundIdentity_IsHttpOutboundClassifierVersion1()
    {
        Assert.Equal("csharp2md.classifier.http-outbound", BoundaryPass.HttpOutboundIdentity.Id);
        Assert.Equal(1, BoundaryPass.HttpOutboundIdentity.Version);
    }

    [Fact]
    [Trait("Requirement", "EBC-17")]
    public void Execute_PublishAsyncOnEventBus_CreatesOutboundMessagingBoundaryWithEventTypeKey()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        AddMessageOperation(pipeline, method.Reference, "PublishAsync", "global::Acme.Shared.Contracts.OrderPlaced", ordinal: 1);
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(BoundaryDirection.Outbound, operation.Direction);
        Assert.Equal(BoundaryProtocol.Messaging, operation.Protocol);
        Assert.Equal(method.Reference, operation.Symbol);
        Assert.NotNull(operation.ProtocolOperationKey);
        Assert.Equal(LiteralRole.ProtocolName, operation.ProtocolOperationKey.Value.Role);
        Assert.Equal("global::Acme.Shared.Contracts.OrderPlaced", operation.ProtocolOperationKey.Value.Value);
    }

    [Fact]
    [Trait("Requirement", "EBC-17")]
    public void Execute_PublishOnEventBus_CreatesOutboundMessagingBoundary()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        AddMessageOperation(pipeline, method.Reference, "Publish", "global::Acme.Shared.Contracts.PaymentProcessed", ordinal: 1);
        var context = new ClassifierContext(pipeline);

        new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(BoundaryProtocol.Messaging, operation.Protocol);
        Assert.Equal(BoundaryDirection.Outbound, operation.Direction);
        Assert.Equal("global::Acme.Shared.Contracts.PaymentProcessed", operation.ProtocolOperationKey!.Value.Value);
    }

    [Fact]
    [Trait("Requirement", "EBC-17")]
    public void Execute_SubscribeMessageOperation_DoesNotCreateOutboundMessagingBoundary()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        AddMessageOperation(pipeline, method.Reference, "Subscribe", "global::Acme.Shared.Contracts.OrderPlaced", ordinal: 1);
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
    }

    [Fact]
    [Trait("Requirement", "EBC-17")]
    public void Execute_PublishAsyncWithoutTypeArgument_RecordsDiagnosticWithoutBoundary()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, method.Reference]);
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                method.Reference,
                ObservationKind.MessageOperation,
                ordinal: 1,
                MessageOperationPayload("PublishAsync", eventType: null)));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
        var diagnostic = Assert.Single(pipeline.Accumulator.ToSnapshot().Diagnostics.ToArray());
        Assert.Equal("missing-message-type-argument", diagnostic.Code);
        Assert.Equal(method.Reference.Id.Value, diagnostic.IdentityOrKey);
    }

    [Fact]
    [Trait("Requirement", "EBC-20")]
    public void MessagingIdentity_IsMessagingClassifierVersion1()
    {
        Assert.Equal("csharp2md.classifier.messaging", BoundaryPass.MessagingIdentity.Id);
        Assert.Equal(1, BoundaryPass.MessagingIdentity.Version);
    }

    private static PipelineContext ArrangeOrders()
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

    private static NormalizedPayload RoutePayload(string template) =>
        NormalizedPayload.Create(
        [
            new PayloadEntry(
                BoundaryPass.RouteKey,
                StructuralLiteral.Create(LiteralRole.Route, template, BoundaryPass.RouteKey)),
        ]);

    private static void AddCreateClient(PipelineContext pipeline, FactReference owner, string clientName, int ordinal) =>
        pipeline.Accumulator.AddObservation(
            CreateObservation(owner, ObservationKind.Invocation, ordinal, CreateClientPayload(clientName)));

    private static void AddHttpInvocation(
        PipelineContext pipeline,
        FactReference owner,
        string methodName,
        string route,
        int ordinal) =>
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                owner,
                ObservationKind.Invocation,
                ordinal,
                NormalizedPayload.Create(
                [
                    new PayloadEntry(
                        BoundaryPass.MethodNameKey,
                        StructuralLiteral.Create(LiteralRole.ProtocolName, methodName, BoundaryPass.MethodNameKey)),
                    new PayloadEntry(
                        BoundaryPass.RouteKey,
                        StructuralLiteral.Create(LiteralRole.Route, route, BoundaryPass.RouteKey)),
                ])));

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

    private static void AddMessageOperation(
        PipelineContext pipeline,
        FactReference owner,
        string methodName,
        string eventType,
        int ordinal) =>
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                owner,
                ObservationKind.MessageOperation,
                ordinal,
                MessageOperationPayload(methodName, eventType)));

    private static NormalizedPayload MessageOperationPayload(string methodName, string? eventType)
    {
        var entries = new List<PayloadEntry>
        {
            new(
                BoundaryPass.MethodNameKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, methodName, BoundaryPass.MethodNameKey)),
            new(
                BoundaryPass.TargetTypeKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, BoundaryPass.EventBusTypeName, BoundaryPass.TargetTypeKey)),
        };
        if (eventType is not null)
        {
            entries.Add(
                new PayloadEntry(
                    BoundaryPass.TypeArgumentKey,
                    StructuralLiteral.Create(LiteralRole.ProtocolName, eventType, BoundaryPass.TypeArgumentKey)));
        }

        return NormalizedPayload.Create(entries);
    }

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
