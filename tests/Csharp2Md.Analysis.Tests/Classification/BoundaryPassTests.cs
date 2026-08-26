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
