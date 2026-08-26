using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ContractPassTests
{
    [Fact]
    [Trait("Requirement", "EBC-21")]
    [Trait("Requirement", "EBC-22")]
    [Trait("Requirement", "EBC-26")]
    public void Execute_OutboundAndInboundMessagingForSharedEventType_CreatesContractBindingsAndRevision()
    {
        var pipeline = Arrange();
        var eventType = AddNamedType(pipeline, "OrderPlaced", "global::Acme.Shared.Contracts", SharedProject);
        AddProperty(pipeline, eventType, "OrderId", "global::System.Guid");
        AddProperty(pipeline, eventType, "Amount", "decimal");
        var publisher = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var handler = AddMethod(pipeline, "HandleAsync", "global::Acme.Orders.Events.OrderPlacedEventHandler", OrdersProject);
        var component = AddComponent(pipeline, [publisher.Reference, handler.Reference]);
        var outbound = AddMessagingBoundary(pipeline, publisher, component, BoundaryDirection.Outbound, OrderPlacedFqn);
        var inbound = AddMessagingBoundary(pipeline, handler, component, BoundaryDirection.Inbound, OrderPlacedFqn);
        var context = new ClassifierContext(pipeline);

        var result = new ContractPass().Execute(context, CancellationToken.None);

        var contract = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Contract>().ToArray());
        Assert.Equal(LiteralRole.ProtocolName, contract.Proof.Role);
        Assert.Equal(OrderPlacedFqn, contract.Proof.Value);
        var bindings = pipeline.Accumulator.ToSnapshot().Facts.OfType<ContractBinding>().ToArray();
        Assert.Equal(2, bindings.Length);
        Assert.Contains(bindings, binding => binding.Operation.Equals(outbound.Reference) && binding.PayloadRole == "request");
        Assert.Contains(bindings, binding => binding.Operation.Equals(inbound.Reference) && binding.PayloadRole == "request");
        Assert.All(bindings, binding => Assert.Equal(eventType.Reference, binding.ClrSymbol));
        Assert.All(bindings, binding => Assert.Equal(contract.Reference, binding.Contract));
        var revision = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<ContractRevision>().ToArray());
        Assert.Equal(contract.Reference, revision.Contract);
        Assert.Contains("Amount:decimal", revision.StructuralFingerprint, StringComparison.Ordinal);
        Assert.Contains("OrderId:global::System.Guid", revision.StructuralFingerprint, StringComparison.Ordinal);
        Assert.Equal(4, result.FactCount);
    }

    [Fact]
    [Trait("Requirement", "EBC-24")]
    public void Execute_PublishOnlyEvent_DoesNotCreateContract()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, "PaymentProcessed", "global::Acme.Shared.Contracts", SharedProject);
        var publisher = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var component = AddComponent(pipeline, [publisher.Reference]);
        AddMessagingBoundary(
            pipeline,
            publisher,
            component,
            BoundaryDirection.Outbound,
            "global::Acme.Shared.Contracts.PaymentProcessed");
        var context = new ClassifierContext(pipeline);

        var result = new ContractPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<Contract>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<ContractBinding>());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<ContractRevision>());
    }

    [Fact]
    [Trait("Requirement", "EBC-25")]
    public void Execute_EventTypeDeclaredInSameProjectAsPublisherAndHandler_DoesNotCreateContract()
    {
        var pipeline = Arrange();
        AddNamedType(pipeline, "LocalEvent", "global::Acme.Orders", OrdersProject);
        var publisher = AddMethod(pipeline, "PublishLocal", "global::Acme.Orders.OrderService", OrdersProject);
        var handler = AddMethod(pipeline, "HandleAsync", "global::Acme.Orders.LocalHandler", OrdersProject);
        var component = AddComponent(pipeline, [publisher.Reference, handler.Reference]);
        AddMessagingBoundary(pipeline, publisher, component, BoundaryDirection.Outbound, "global::Acme.Orders.LocalEvent");
        AddMessagingBoundary(pipeline, handler, component, BoundaryDirection.Inbound, "global::Acme.Orders.LocalEvent");
        var context = new ClassifierContext(pipeline);

        new ContractPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<Contract>());
    }

    [Fact]
    [Trait("Requirement", "EBC-21")]
    public void Execute_AnonymousEventType_DoesNotCreateContract()
    {
        var pipeline = Arrange();
        var publisher = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var handler = AddMethod(pipeline, "HandleAsync", "global::Acme.Orders.Events.OrderPlacedEventHandler", OrdersProject);
        var component = AddComponent(pipeline, [publisher.Reference, handler.Reference]);
        const string anonymous = "global::<>f__AnonymousType0";
        AddMessagingBoundary(pipeline, publisher, component, BoundaryDirection.Outbound, anonymous);
        AddMessagingBoundary(pipeline, handler, component, BoundaryDirection.Inbound, anonymous);
        var context = new ClassifierContext(pipeline);

        new ContractPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<Contract>());
    }

    [Fact]
    [Trait("Requirement", "EBC-21")]
    public void ContractMessagingIdentity_IsContractMessagingClassifierVersion1()
    {
        Assert.Equal("csharp2md.classifier.contract-messaging", ContractPass.Identity.Id);
        Assert.Equal(1, ContractPass.Identity.Version);
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
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

    private static void AddProperty(PipelineContext pipeline, Symbol declaringType, string metadata, string propertyType)
    {
        var container = ReadTypeName(declaringType);
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("property", container, metadata, 0, propertyType),
            declaringType.OwningProject,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
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

    private static BoundaryOperation AddMessagingBoundary(
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

    private static string ReadTypeName(Symbol type)
    {
        const string marker = ";type=";
        var identity = type.Signature.Value;
        var start = identity.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = identity.IndexOf(';', start);
        var encoded = end < 0 ? identity[start..] : identity[start..end];
        return Uri.UnescapeDataString(encoded);
    }
}
