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

public sealed class EntryPointPassTests
{
    [Fact]
    [Trait("Requirement", "EBC-05")]
    public void Execute_CallableOnControllerBaseDescendant_CreatesEntryPointOwnedByProjectComponent()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        AddBaseType(pipeline, controller.Reference);
        pipeline.Accumulator.AddObservation(CreateObservation(action.Reference, ObservationKind.RouteDeclaration, ordinal: 1));
        AddComponent(pipeline, [controller.Reference, action.Reference]);
        var context = new ClassifierContext(pipeline);

        var result = new EntryPointPass().Execute(context, CancellationToken.None);

        var entry = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(action.Reference, entry.Symbol);
        Assert.Equal(
            Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", [controller.Reference, action.Reference]).Reference,
            entry.OwningComponent);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "EBC-08")]
    public void Execute_ControllerActionWithoutRoute_CreatesEntryPointAndDiagnosticWithoutBoundaryOperation()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        AddBaseType(
            pipeline,
            controller.Reference,
            NormalizedPayload.Create(
            [
                new PayloadEntry(
                    EntryPointPass.TargetTypeKey,
                    StructuralLiteral.Create(LiteralRole.ProtocolName, EntryPointPass.ControllerBaseTypeName, "target-type")),
            ]));
        AddComponent(pipeline, [controller.Reference, action.Reference]);
        var context = new ClassifierContext(pipeline);

        new EntryPointPass().Execute(context, CancellationToken.None);

        var entry = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>().ToArray());
        Assert.Equal(action.Reference, entry.Symbol);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
        var diagnostic = Assert.Single(pipeline.Accumulator.ToSnapshot().Diagnostics.ToArray());
        Assert.Equal("missing-route-declaration", diagnostic.Code);
        Assert.Contains("GetOrderStatus", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(action.Reference.Id.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(action.Reference.Id.Value, diagnostic.IdentityOrKey);
    }

    [Fact]
    [Trait("Requirement", "EBC-09")]
    public void Execute_CallableOnNonControllerNonHandler_DoesNotCreateEntryPoint()
    {
        var pipeline = ArrangeOrders();
        var service = AddNamedType(pipeline, "OrderService", "global::Acme.Orders", OrdersProject);
        var method = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        pipeline.Accumulator.AddObservation(CreateObservation(method.Reference, ObservationKind.Invocation, ordinal: 1));
        AddComponent(pipeline, [service.Reference, method.Reference]);
        var context = new ClassifierContext(pipeline);

        var result = new EntryPointPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>());
    }

    [Fact]
    [Trait("Requirement", "EBC-09")]
    public void Execute_ControllerBaseDescendantWithNoCallableActions_DoesNotCreateEntryPoint()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "EmptyController", "global::Acme.Orders.Api", OrdersProject);
        AddBaseType(
            pipeline,
            controller.Reference,
            NormalizedPayload.Create(
            [
                new PayloadEntry(
                    EntryPointPass.TargetTypeKey,
                    StructuralLiteral.Create(LiteralRole.ProtocolName, EntryPointPass.ControllerBaseTypeName, "target-type")),
            ]));
        AddComponent(pipeline, [controller.Reference]);
        var context = new ClassifierContext(pipeline);

        var result = new EntryPointPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>());
    }

    [Fact]
    [Trait("Requirement", "EBC-19")]
    public void Execute_HandleAsyncOnIntegrationEventHandler_CreatesEntryPoint()
    {
        var pipeline = ArrangeOrders();
        var handler = AddNamedType(pipeline, "OrderPlacedEventHandler", "global::Acme.Orders.Events", OrdersProject);
        var method = AddMethod(pipeline, "HandleAsync", "global::Acme.Orders.Events.OrderPlacedEventHandler", OrdersProject);
        AddBaseType(pipeline, handler.Reference);
        AddComponent(pipeline, [handler.Reference, method.Reference]);
        var context = new ClassifierContext(pipeline);

        var result = new EntryPointPass().Execute(context, CancellationToken.None);

        var entry = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(method.Reference, entry.Symbol);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<BoundaryOperation>());
    }

    [Fact]
    [Trait("Requirement", "GCPC-020")]
    public void Execute_PrivateHelperOnRecognizedController_DoesNotCreateEntryPoint()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var helper = AddMethod(
            pipeline, "BuildUri", "global::Acme.Orders.Api.OrdersController", OrdersProject, externallyReachable: false);
        AddBaseType(
            pipeline,
            controller.Reference,
            NormalizedPayload.Create(
            [
                new PayloadEntry(
                    EntryPointPass.TargetTypeKey,
                    StructuralLiteral.Create(LiteralRole.ProtocolName, EntryPointPass.ControllerBaseTypeName, "target-type")),
            ]));
        AddComponent(pipeline, [controller.Reference, helper.Reference]);
        var context = new ClassifierContext(pipeline);

        // GCPC-020: not externally reachable -- private, regardless of the declaring type -- so it
        // never becomes an EntryPoint, exactly the audit's ChangeUriPlaceholder regression.
        var result = new EntryPointPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "GCPC-024")]
    public void Execute_ControllerActionWithNoResolvableOwningComponent_PublishesUnresolvedNotEntryPoint()
    {
        var pipeline = ArrangeOrders();
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Orders.Api", OrdersProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        AddBaseType(pipeline, controller.Reference);
        pipeline.Accumulator.AddObservation(CreateObservation(action.Reference, ObservationKind.RouteDeclaration, ordinal: 1));
        // Deliberately no AddComponent call: the action is a reachable, routed controller action --
        // positive entry evidence -- but no Component fact covers it, so ownership cannot be resolved.
        var context = new ClassifierContext(pipeline);

        var result = new EntryPointPass().Execute(context, CancellationToken.None);

        // GCPC-024: capability is otherwise positively indicated, but the unresolved ownership means
        // it is published as unresolved instead of a confirmed EntryPoint.
        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>());
        Assert.Equal(1, result.UnresolvedCount);
        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(action.Reference, unresolved.Source);
        Assert.Equal(RelationKind.Executes, unresolved.Kind);
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        Assert.Contains(unresolved.Available.DerivedFrom, identity => identity.Kind is ObservationKind.RouteDeclaration);
    }

    [Fact]
    [Trait("Requirement", "EBC-05")]
    public void Identity_IsEntrypointClassifierVersion1()
    {
        Assert.Equal("csharp2md.classifier.entrypoint", EntryPointPass.Identity.Id);
        Assert.Equal(1, EntryPointPass.Identity.Version);
    }

    [Fact]
    [Trait("Requirement", "CDC-14")]
    public void Execute_ControllerActionInPrivatelyUsedLibrary_ResolvesApplicationComponent()
    {
        var pipeline = ArrangeOrders();
        pipeline.Accumulator.AddFact(Project.Create(ContractsProject));
        var controller = AddNamedType(pipeline, "OrdersController", "global::Acme.Shared.Contracts.Api", ContractsProject);
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Shared.Contracts.Api.OrdersController", ContractsProject);
        AddBaseType(pipeline, controller.Reference);
        pipeline.Accumulator.AddObservation(CreateObservation(action.Reference, ObservationKind.RouteDeclaration, ordinal: 1));
        var component = Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", [controller.Reference, action.Reference]);
        pipeline.Accumulator.AddFact(component);
        var context = new ClassifierContext(pipeline);

        var result = new EntryPointPass().Execute(context, CancellationToken.None);

        var entry = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<EntryPoint>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(action.Reference, entry.Symbol);
        Assert.Equal(component.Reference, entry.OwningComponent);
        Assert.Equal("Acme.Orders/Acme.Orders.csproj", component.Name);
        Assert.NotEqual("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", component.Name);
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

    private static Symbol AddMethod(PipelineContext pipeline, string metadata, string container, ProjectId project) =>
        AddMethod(pipeline, metadata, container, project, externallyReachable: true);

    private static Symbol AddMethod(
        PipelineContext pipeline, string metadata, string container, ProjectId project, bool externallyReachable)
    {
        var facets = externallyReachable
            ? new[] { SymbolFacet.Callable, SymbolFacet.ExternallyReachable }
            : new[] { SymbolFacet.Callable };
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", container, metadata, 0, "global::System.Void"),
            project,
            SymbolFacetSet.Create(facets));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static void AddBaseType(PipelineContext pipeline, FactReference owner, NormalizedPayload? payload = null) =>
        pipeline.Accumulator.AddObservation(
            CreateObservation(owner, ObservationKind.BaseType, ordinal: 1, payload ?? NormalizedPayload.Create([])));

    private static void AddComponent(PipelineContext pipeline, IEnumerable<FactReference> owners) =>
        pipeline.Accumulator.AddFact(Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", owners));

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
