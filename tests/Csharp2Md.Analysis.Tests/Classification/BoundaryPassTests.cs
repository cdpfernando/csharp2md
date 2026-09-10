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
    [Trait("Requirement", "GCPC-099")]
    [Trait("Requirement", "GCPC-100")]
    public void Execute_RouteDeclarationWithVerbAndTemplate_PublishesBothHttpMethodAndRouteFields()
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
                RoutePayload("orders/{id}", "GET")));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(1, result.FactCount);
        // GCPC-099: both proven fields are published on their own fields ...
        Assert.Equal("GET", operation.HttpMethod);
        Assert.NotNull(operation.Route);
        Assert.Equal(LiteralRole.Route, operation.Route.Value.Role);
        Assert.Equal("orders/{id}", operation.Route.Value.Value);
        // GCPC-100: ... and protocolOperationKey is not the only place either was published.
        Assert.NotNull(operation.ProtocolOperationKey);
        Assert.Equal("GET orders/{id}", operation.ProtocolOperationKey.Value.Value);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
    }

    [Fact]
    [Trait("Requirement", "GCPC-102")]
    public void Execute_RouteDeclarationWithVerbButNoTemplate_PublishesTheVerbAndRecordsRouteAsUnresolved()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        var routeDeclaration = CreateObservation(
            action.Reference,
            ObservationKind.RouteDeclaration,
            ordinal: 1,
            VerbOnlyPayload("GET"));
        pipeline.Accumulator.AddObservation(routeDeclaration);
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        // The verb is proven (a bare verb attribute with no template argument, i.e. convention-derived
        // route) and published; the route itself is unresolved rather than silently absent.
        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal("GET", operation.HttpMethod);
        Assert.Null(operation.Route);
        Assert.Equal(1, result.UnresolvedCount);
        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(operation.Reference, unresolved.Source);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.Contains(unresolved.Available.DerivedFrom, identity => identity.Equals(routeDeclaration.Identity));
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "GCPC-102")]
    public void Execute_RouteDeclarationWithTemplateButNoVerb_PublishesTheRouteAndRecordsVerbAsUnresolved()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        var routeDeclaration = CreateObservation(
            action.Reference,
            ObservationKind.RouteDeclaration,
            ordinal: 1,
            RoutePayload("orders/{id}"));
        pipeline.Accumulator.AddObservation(routeDeclaration);
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        // Symmetric to the verb-only case: a verb-agnostic [Route] attribute proves the route but not
        // the verb (GCPC-102 covers either unproven part, not only the route).
        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Null(operation.HttpMethod);
        Assert.NotNull(operation.Route);
        Assert.Equal("orders/{id}", operation.Route.Value.Value);
        Assert.Equal(1, result.UnresolvedCount);
        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(operation.Reference, unresolved.Source);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
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
    [Trait("Requirement", "EBC-31")]
    public void Execute_GetAndDeleteSameRouteTemplate_CreatesTwoInboundHttpBoundariesWithoutCorruption()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var get = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var delete = AddMethod(pipeline, "DeleteOrder", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, get.Reference, delete.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(get.Reference, component.Reference));
        pipeline.Accumulator.AddFact(EntryPoint.Create(delete.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(
            CreateObservation(get.Reference, ObservationKind.RouteDeclaration, ordinal: 1, RoutePayload("{id:int}", "GET")));
        pipeline.Accumulator.AddObservation(
            CreateObservation(delete.Reference, ObservationKind.RouteDeclaration, ordinal: 1, RoutePayload("{id:int}", "DELETE")));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.False(pipeline.Accumulator.StructuralCorruption);
        Assert.Null(pipeline.Accumulator.CollidingIdentity);
        Assert.Equal(2, result.FactCount);
        var operations = pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray();
        Assert.Equal(2, operations.Length);
        Assert.Contains(operations, operation => operation.ProtocolOperationKey?.Value == "GET {id:int}");
        Assert.Contains(operations, operation => operation.ProtocolOperationKey?.Value == "DELETE {id:int}");
    }

    [Fact]
    [Trait("Requirement", "EBC-06")]
    [Trait("Requirement", "EBC-31")]
    public void Execute_TwoEntryPointsWithSameGetRoute_CreatesOneInboundHttpBoundaryWithoutCorruption()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var first = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var second = AddMethod(pipeline, "GetOrderById", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [controller.Reference, first.Reference, second.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(first.Reference, component.Reference));
        pipeline.Accumulator.AddFact(EntryPoint.Create(second.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(
            CreateObservation(first.Reference, ObservationKind.RouteDeclaration, ordinal: 1, RoutePayload("{id:int}", "GET")));
        pipeline.Accumulator.AddObservation(
            CreateObservation(second.Reference, ObservationKind.RouteDeclaration, ordinal: 1, RoutePayload("{id:int}", "GET")));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.False(pipeline.Accumulator.StructuralCorruption);
        Assert.Null(pipeline.Accumulator.CollidingIdentity);
        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal("GET {id:int}", operation.ProtocolOperationKey!.Value.Value);
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
    [Trait("Requirement", "EBC-31")]
    public void Execute_TwoPublishAsyncOfSameEventInOneComponent_CreatesOneOutboundMessagingBoundaryWithoutCorruption()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var first = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var second = AddMethod(pipeline, "RetryPlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddComponent(pipeline, [service.Reference, first.Reference, second.Reference]);
        AddMessageOperation(pipeline, first.Reference, "PublishAsync", "global::Acme.Shared.Contracts.OrderPlaced", ordinal: 1);
        AddMessageOperation(pipeline, second.Reference, "PublishAsync", "global::Acme.Shared.Contracts.OrderPlaced", ordinal: 1);
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        Assert.False(pipeline.Accumulator.StructuralCorruption);
        Assert.Null(pipeline.Accumulator.CollidingIdentity);
        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(BoundaryDirection.Outbound, operation.Direction);
        Assert.Equal(BoundaryProtocol.Messaging, operation.Protocol);
        Assert.Equal("global::Acme.Shared.Contracts.OrderPlaced", operation.ProtocolOperationKey!.Value.Value);
        Assert.Equal(first.Reference, operation.Symbol);
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

    [Fact]
    [Trait("Requirement", "EBC-18")]
    public void Execute_HandleAsyncOnIntegrationEventHandler_CreatesInboundMessagingBoundary()
    {
        var pipeline = ArrangeOrders();
        var handler = AddNamedType(pipeline, "OrderPlacedEventHandler", "global::Acme.Orders.Events", OrdersProject);
        var method = AddHandleAsync(pipeline, "global::Acme.Orders.Events.OrderPlacedEventHandler", OrdersProject);
        var component = AddComponent(pipeline, [handler.Reference, method.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(method.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                handler.Reference,
                ObservationKind.BaseType,
                ordinal: 1,
                NormalizedPayload.Create(
                [
                    new PayloadEntry(
                        BoundaryPass.TargetTypeKey,
                        StructuralLiteral.Create(
                            LiteralRole.ProtocolName,
                            BoundaryPass.IntegrationEventHandlerTypeName,
                            BoundaryPass.TargetTypeKey)),
                    new PayloadEntry(
                        BoundaryPass.TypeArgumentKey,
                        StructuralLiteral.Create(
                            LiteralRole.ProtocolName,
                            "global::Acme.Shared.Contracts.OrderPlaced",
                            BoundaryPass.TypeArgumentKey)),
                ])));
        var context = new ClassifierContext(pipeline);

        var result = new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(BoundaryDirection.Inbound, operation.Direction);
        Assert.Equal(BoundaryProtocol.Messaging, operation.Protocol);
        Assert.Equal(method.Reference, operation.Symbol);
        Assert.Equal("global::Acme.Shared.Contracts.OrderPlaced", operation.ProtocolOperationKey!.Value.Value);
    }

    [Fact]
    [Trait("Requirement", "CDC-14")]
    public void Execute_OutboundHttpInPrivatelyUsedLibrary_ResolvesApplicationComponent()
    {
        var pipeline = ArrangeOrders();
        pipeline.Accumulator.AddFact(Project.Create(ContractsProject));
        var service = AddNamedType(pipeline, "PaymentClient", "global::Acme.Shared.Contracts", ContractsProject);
        var method = AddMethod(pipeline, "AuthorizeAsync", "global::Acme.Shared.Contracts.PaymentClient", ContractsProject);
        var component = Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", [service.Reference, method.Reference]);
        pipeline.Accumulator.AddFact(component);
        AddCreateClient(pipeline, method.Reference, "PaymentService", ordinal: 1);
        AddHttpInvocation(pipeline, method.Reference, "PostAsJsonAsync", "payments/authorize", ordinal: 2);
        var context = new ClassifierContext(pipeline);

        new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(BoundaryDirection.Outbound, operation.Direction);
        Assert.Equal(BoundaryProtocol.Http, operation.Protocol);
        Assert.Equal(component.Reference, operation.OwningComponent);
        Assert.Equal("Acme.Orders/Acme.Orders.csproj", component.Name);
        Assert.NotEqual("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", component.Name);
        Assert.Equal(method.Reference, operation.Symbol);
    }

    [Fact]
    [Trait("Requirement", "CDC-14")]
    public void Execute_OutboundMessagingInPrivatelyUsedLibrary_ResolvesApplicationComponent()
    {
        var pipeline = ArrangeOrders();
        pipeline.Accumulator.AddFact(Project.Create(ContractsProject));
        var service = AddNamedType(pipeline, "OrderPublisher", "global::Acme.Shared.Contracts", ContractsProject);
        var method = AddMethod(pipeline, "PublishOrderPlacedAsync", "global::Acme.Shared.Contracts.OrderPublisher", ContractsProject);
        var component = Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", [service.Reference, method.Reference]);
        pipeline.Accumulator.AddFact(component);
        AddMessageOperation(pipeline, method.Reference, "PublishAsync", "global::Acme.Shared.Contracts.OrderPlaced", ordinal: 1);
        var context = new ClassifierContext(pipeline);

        new BoundaryPass().Execute(context, CancellationToken.None);

        var operation = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>().ToArray());
        Assert.Equal(BoundaryDirection.Outbound, operation.Direction);
        Assert.Equal(BoundaryProtocol.Messaging, operation.Protocol);
        Assert.Equal(component.Reference, operation.OwningComponent);
        Assert.Equal("Acme.Orders/Acme.Orders.csproj", component.Name);
        Assert.NotEqual("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", component.Name);
        Assert.Equal(method.Reference, operation.Symbol);
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

    private static ProjectId ContractsProject =>
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

    private static Symbol AddHandleAsync(PipelineContext pipeline, string container, ProjectId project)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                container,
                "HandleAsync",
                0,
                "global::System.Threading.Tasks.Task",
                [
                    new SymbolParameterSignature("global::Acme.Shared.Contracts.OrderPlaced"),
                    new SymbolParameterSignature("global::System.Threading.CancellationToken"),
                ]),
            project,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static NormalizedPayload RoutePayload(string template, string? httpMethod = null)
    {
        var entries = new List<PayloadEntry>
        {
            new(
                BoundaryPass.RouteKey,
                StructuralLiteral.Create(LiteralRole.Route, template, BoundaryPass.RouteKey)),
        };
        if (httpMethod is not null)
        {
            entries.Add(
                new PayloadEntry(
                    BoundaryPass.MethodNameKey,
                    StructuralLiteral.Create(LiteralRole.ProtocolName, httpMethod, BoundaryPass.MethodNameKey)));
        }

        return NormalizedPayload.Create(entries);
    }

    /// <summary>
    /// A route declaration observation carrying a verb but no route key at all -- e.g. a bare verb
    /// attribute with no template argument, matching <c>RouteDeclarationDetector</c>'s real payload
    /// shape for that case (GCPC-102).
    /// </summary>
    private static NormalizedPayload VerbOnlyPayload(string httpMethod) =>
        NormalizedPayload.Create(
        [
            new PayloadEntry(
                BoundaryPass.MethodNameKey,
                StructuralLiteral.Create(LiteralRole.ProtocolName, httpMethod, BoundaryPass.MethodNameKey)),
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
