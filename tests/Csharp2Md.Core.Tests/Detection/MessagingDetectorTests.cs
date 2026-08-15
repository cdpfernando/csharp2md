using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Tests.Detection;

public sealed class MessagingDetectorTests
{
    /// <summary>Mirrors the call shapes the synthetic fixture actually uses.</summary>
    private static IReadOnlyList<DependencySignal> Detect(string body, bool withSemantics = false)
    {
        var source = $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Acme.Orders;

            public sealed record OrderPlaced(Guid OrderId);

            public sealed record PaymentProcessed(Guid PaymentId);

            public interface IEventBus
            {
                Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default);

                void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler);
            }

            public sealed class OrderService
            {
                private readonly IEventBus _bus = null!;

                public async Task Run()
                {
            {{body}}
                }

                private Task Handle(OrderPlaced placed, CancellationToken token) => Task.CompletedTask;
            }

            """;

        return new MessagingDetector()
            .Detect(DetectionSource.DocumentContext(source, withSemantics: withSemantics))
            .ToList();
    }

    // P2-03: a publish records a half-edge carrying topic and role, never a finished edge.
    [Fact]
    public void Detect_PublishOfAConstructedMessage_RecordsAPublishHalfEdge()
    {
        var signals = Detect("            await _bus.PublishAsync(new OrderPlaced(Guid.NewGuid()));");

        var signal = Assert.Single(signals);
        Assert.Equal(MessagingRole.Publish, signal.Role);
        Assert.Equal("OrderPlaced", signal.RawTarget);
        Assert.Equal(DependencyKind.Messaging, signal.Kind);
        Assert.Null(signal.TargetService);
    }

    [Fact]
    public void Detect_SubscribeWithAnExplicitTypeArgument_RecordsASubscribeHalfEdge()
    {
        var signals = Detect("            _bus.Subscribe<OrderPlaced>(Handle);");

        var signal = Assert.Single(signals);
        Assert.Equal(MessagingRole.Subscribe, signal.Role);
        Assert.Equal("OrderPlaced", signal.RawTarget);
        Assert.Null(signal.TargetService);
    }

    [Fact]
    public void Detect_CommunicationTypeOfEveryMessagingSignal_IsPubSubEvento()
    {
        var signals = Detect("            await _bus.PublishAsync(new OrderPlaced(Guid.NewGuid()));");

        Assert.Equal(CommunicationType.PubSubEvento, Assert.Single(signals).Communication);
    }

    [Fact]
    public void Detect_DocumentThatBothPublishesAndSubscribes_RecordsBothHalfEdges()
    {
        var signals = Detect(
            """
                        _bus.Subscribe<OrderPlaced>(Handle);
                        await _bus.PublishAsync(new PaymentProcessed(Guid.NewGuid()));
            """);

        Assert.Equal(2, signals.Count);
        Assert.Equal(
            [(MessagingRole.Subscribe, "OrderPlaced"), (MessagingRole.Publish, "PaymentProcessed")],
            signals.Select(s => (s.Role, s.RawTarget)));
    }

    // Correlation happens in Stage 3, so nothing is resolved at detection time (feeds P2-15).
    [Fact]
    public void Detect_SignalResolution_IsUnresolvedUntilTheGraphIsAssembled()
    {
        var signals = Detect("            await _bus.PublishAsync(new OrderPlaced(Guid.NewGuid()));");

        Assert.Equal(ResolutionKind.Unresolved, Assert.Single(signals).Resolution);
    }

    [Fact]
    public void Detect_PublishOfAVariable_ResolvesTheTopicThroughTheSemanticModel()
    {
        var signals = Detect(
            """
                        var message = new PaymentProcessed(Guid.NewGuid());
                        await _bus.PublishAsync(message);
            """,
            withSemantics: true);

        Assert.Equal("PaymentProcessed", Assert.Single(signals).RawTarget);
    }

    // Without a model and without an explicit type, the topic is genuinely unknown: recording a
    // signal with a guessed topic would create a false correlation in the graph.
    [Fact]
    public void Detect_PublishOfAVariableWithNoSemanticModel_RecordsNothing()
    {
        var signals = Detect(
            """
                        var message = new PaymentProcessed(Guid.NewGuid());
                        await _bus.PublishAsync(message);
            """);

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_RecordsTheSourceServiceAndCallSite()
    {
        var signals = Detect("            await _bus.PublishAsync(new OrderPlaced(Guid.NewGuid()));");

        var signal = Assert.Single(signals);
        Assert.Equal(new ServiceName("Acme.Orders"), signal.SourceService);
        Assert.Equal("Orders/Gateway.cs", signal.Location.FilePath);
        Assert.Equal(24, signal.Location.Line);
    }

    [Fact]
    public void Detect_InvocationThatIsNotAPublishOrSubscribe_RecordsNothing()
    {
        Assert.Empty(Detect("            await Task.Delay(0);", withSemantics: true));
    }
}
