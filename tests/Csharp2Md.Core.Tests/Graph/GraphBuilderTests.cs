using Csharp2Md.Core;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Tests.Graph;

public sealed class GraphBuilderTests
{
    private static DependencySignal Messaging(string service, string topic, MessagingRole role, int line = 1) =>
        new(SourceService: new ServiceName(service),
            TargetService: null,
            RawTarget: topic,
            Kind: DependencyKind.Messaging,
            Communication: CommunicationType.PubSubEvento,
            Resolution: ResolutionKind.Unresolved,
            Role: role,
            Location: new SourceLocation($"{service}/Handler.cs", line));

    private static DependencySignal Http(
        string service,
        string rawTarget,
        ResolutionKind resolution = ResolutionKind.HardCoded,
        CommunicationType communication = CommunicationType.SincronoBloqueante,
        int line = 1) =>
        new(SourceService: new ServiceName(service),
            TargetService: null,
            RawTarget: rawTarget,
            Kind: DependencyKind.Http,
            Communication: communication,
            Resolution: resolution,
            Role: null,
            Location: new SourceLocation($"{service}/Client.cs", line));

    // P2-14: a publish of topic T in service A pairs with a subscribe of T in service B to produce
    // one edge, directed publisher -> subscriber.
    [Fact]
    public void Build_PublishPairedWithSubscribeInAnotherService_ProducesOneEdgeFromPublisherToSubscriber()
    {
        var graph = GraphBuilder.Build(
        [
            Messaging("Acme.Orders", "OrderPlaced", MessagingRole.Publish),
            Messaging("Acme.Payments", "OrderPlaced", MessagingRole.Subscribe),
        ]);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(new ServiceName("Acme.Orders"), edge.Source);
        Assert.Equal(new ServiceName("Acme.Payments"), edge.Target);
        Assert.Equal(CommunicationType.PubSubEvento, edge.Communication);

        // Spec-precision gap: P2-14 does not state a correlated edge's resolution. NotApplicable,
        // because nothing resolved through config here — the pairing was made on topic name. It is
        // specifically *not* Unresolved, which P2-15 reserves for signals that found no counterpart.
        Assert.Equal(ResolutionKind.NotApplicable, edge.Resolution);
    }

    // P2-14: the correlated edge's evidence is both halves — the publish site and the subscribe site.
    [Fact]
    public void Build_CorrelatedPair_AggregatesEvidenceFromBothHalves()
    {
        var graph = GraphBuilder.Build(
        [
            Messaging("Acme.Orders", "OrderPlaced", MessagingRole.Publish, line: 42),
            Messaging("Acme.Payments", "OrderPlaced", MessagingRole.Subscribe, line: 17),
        ]);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(
            new[]
            {
                new SourceLocation("Acme.Orders/Handler.cs", 42),
                new SourceLocation("Acme.Payments/Handler.cs", 17),
            },
            edge.Evidence);
    }

    // P2-15: an unpaired publish is retained, targeting the topic name, resolution Unresolved.
    [Fact]
    public void Build_PublishWithNoSubscriberAnywhere_IsRetainedAsAnUnresolvedEdgeTargetingTheTopic()
    {
        var graph = GraphBuilder.Build([Messaging("Acme.Payments", "PaymentProcessed", MessagingRole.Publish)]);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(new ServiceName("Acme.Payments"), edge.Source);
        Assert.Equal(new ServiceName("PaymentProcessed"), edge.Target);
        Assert.Equal(CommunicationType.PubSubEvento, edge.Communication);
        Assert.Equal(ResolutionKind.Unresolved, edge.Resolution);
    }

    // P2-15: the same retention rule holds for a subscribe with no publisher.
    [Fact]
    public void Build_SubscribeWithNoPublisherAnywhere_IsRetainedAsAnUnresolvedEdgeTargetingTheTopic()
    {
        var graph = GraphBuilder.Build([Messaging("Acme.Payments", "InventoryLow", MessagingRole.Subscribe)]);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(new ServiceName("Acme.Payments"), edge.Source);
        Assert.Equal(new ServiceName("InventoryLow"), edge.Target);
        Assert.Equal(ResolutionKind.Unresolved, edge.Resolution);
    }

    // P2-14 pairs across *different* services, so publishing and subscribing to one topic inside a
    // single service is not a pairing — both halves stay unpaired rather than forming a self-edge.
    [Fact]
    public void Build_PublishAndSubscribeOfOneTopicInsideTheSameService_ProducesNoSelfEdge()
    {
        var graph = GraphBuilder.Build(
        [
            Messaging("Acme.Orders", "OrderPlaced", MessagingRole.Publish, line: 3),
            Messaging("Acme.Orders", "OrderPlaced", MessagingRole.Subscribe, line: 9),
        ]);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(new ServiceName("Acme.Orders"), edge.Source);
        Assert.Equal(new ServiceName("OrderPlaced"), edge.Target);
        Assert.Equal(ResolutionKind.Unresolved, edge.Resolution);
        Assert.Equal(
            new[]
            {
                new SourceLocation("Acme.Orders/Handler.cs", 3),
                new SourceLocation("Acme.Orders/Handler.cs", 9),
            },
            edge.Evidence);
    }

    // P2-14, listed edge case: two services publishing one topic both reach the subscriber.
    [Fact]
    public void Build_MultiplePublishersOfOneTopic_ProducesOneEdgePerPublisherToTheSubscriber()
    {
        var graph = GraphBuilder.Build(
        [
            Messaging("Acme.Orders", "OrderPlaced", MessagingRole.Publish),
            Messaging("Acme.Returns", "OrderPlaced", MessagingRole.Publish),
            Messaging("Acme.Payments", "OrderPlaced", MessagingRole.Subscribe),
        ]);

        Assert.Equal(
            new[]
            {
                (new ServiceName("Acme.Orders"), new ServiceName("Acme.Payments")),
                (new ServiceName("Acme.Returns"), new ServiceName("Acme.Payments")),
            },
            graph.Edges.Select(edge => (edge.Source, edge.Target)));
    }

    // P2-15 is absolute: a messaging signal is retained whatever its role. One carrying no role at
    // all has no counterpart by construction, so it must still surface as a topic-targeted edge
    // rather than vanish between the publish and subscribe passes.
    [Fact]
    public void Build_MessagingSignalCarryingNoRole_IsStillRetainedAsAnEdge()
    {
        var roleless = Messaging("Acme.Orders", "OrderPlaced", MessagingRole.Publish) with { Role = null };

        var edge = Assert.Single(GraphBuilder.Build([roleless]).Edges);

        Assert.Equal(new ServiceName("Acme.Orders"), edge.Source);
        Assert.Equal(new ServiceName("OrderPlaced"), edge.Target);
        Assert.Equal(ResolutionKind.Unresolved, edge.Resolution);
    }

    // P2-14 correlates on the topic name; two different topics never pair with each other.
    [Fact]
    public void Build_PublishAndSubscribeOfDifferentTopics_DoNotCorrelate()
    {
        var graph = GraphBuilder.Build(
        [
            Messaging("Acme.Orders", "OrderPlaced", MessagingRole.Publish),
            Messaging("Acme.Payments", "PaymentProcessed", MessagingRole.Subscribe),
        ]);

        Assert.Equal(
            new[]
            {
                (new ServiceName("Acme.Orders"), new ServiceName("OrderPlaced")),
                (new ServiceName("Acme.Payments"), new ServiceName("PaymentProcessed")),
            },
            graph.Edges.Select(edge => (edge.Source, edge.Target)));
        Assert.All(graph.Edges, edge => Assert.Equal(ResolutionKind.Unresolved, edge.Resolution));
    }

    // AD-005: a resolved HTTP signal's RawTarget becomes the edge target verbatim, with its
    // communication type and resolution classification intact.
    [Fact]
    public void Build_HttpSignal_PassesThroughWithRawTargetCommunicationAndResolutionIntact()
    {
        var graph = GraphBuilder.Build(
            [Http("Acme.Orders", "http://payments:8080", ResolutionKind.HardCoded)]);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(new ServiceName("Acme.Orders"), edge.Source);
        Assert.Equal(new ServiceName("http://payments:8080"), edge.Target);
        Assert.Equal(CommunicationType.SincronoBloqueante, edge.Communication);
        Assert.Equal(ResolutionKind.HardCoded, edge.Resolution);
        Assert.Equal([new SourceLocation("Acme.Orders/Client.cs", 1)], edge.Evidence);
    }

    // P2-04: where the detector already knew the target service (direct reference), that wins over
    // the raw target string.
    [Fact]
    public void Build_SignalCarryingATargetService_UsesItRatherThanTheRawTarget()
    {
        var signal = new DependencySignal(
            SourceService: new ServiceName("Acme.Orders"),
            TargetService: new ServiceName("Acme.Shared.Contracts"),
            RawTarget: @"..\Acme.Shared.Contracts\Acme.Shared.Contracts.csproj",
            Kind: DependencyKind.DirectReference,
            Communication: CommunicationType.DirectReference,
            Resolution: ResolutionKind.NotApplicable,
            Role: null,
            Location: new SourceLocation("Acme.Orders/Acme.Orders.csproj", 5));

        var edge = Assert.Single(GraphBuilder.Build([signal]).Edges);

        Assert.Equal(new ServiceName("Acme.Shared.Contracts"), edge.Target);
        Assert.Equal(CommunicationType.DirectReference, edge.Communication);
        Assert.Equal(ResolutionKind.NotApplicable, edge.Resolution);
    }

    // "Evidence locations aggregated per edge": repeated calls to one target are one edge carrying
    // every call site, not one edge per call site.
    [Fact]
    public void Build_RepeatedSignalsForTheSameRelationship_CollapseToOneEdgeCarryingEveryLocation()
    {
        var graph = GraphBuilder.Build(
        [
            Http("Acme.Orders", "PaymentService", line: 12),
            Http("Acme.Orders", "PaymentService", line: 30),
        ]);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(
            new[]
            {
                new SourceLocation("Acme.Orders/Client.cs", 12),
                new SourceLocation("Acme.Orders/Client.cs", 30),
            },
            edge.Evidence);
    }

    // Two calls to one target that differ in communication type are genuinely different edges:
    // P2-12 makes communication type part of what an edge *is*.
    [Fact]
    public void Build_SameTargetWithDifferentCommunicationTypes_StaysTwoEdges()
    {
        var graph = GraphBuilder.Build(
        [
            Http("Acme.Orders", "PaymentService", communication: CommunicationType.SincronoBloqueante),
            Http("Acme.Orders", "PaymentService", communication: CommunicationType.AssincronoFireAndForget),
        ]);

        Assert.Equal(
            new[] { CommunicationType.SincronoBloqueante, CommunicationType.AssincronoFireAndForget },
            graph.Edges.Select(edge => edge.Communication).Order());
        Assert.All(graph.Edges, edge => Assert.Equal(new ServiceName("PaymentService"), edge.Target));
    }

    [Fact]
    public void Build_NoSignals_ProducesAnEmptyGraph()
    {
        Assert.Empty(GraphBuilder.Build([]).Edges);
    }
}
