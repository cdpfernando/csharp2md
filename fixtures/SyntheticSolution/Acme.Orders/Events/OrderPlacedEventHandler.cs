// Integration-event handler for Acme.Orders.
//
// IIntegrationEventHandler<TEvent> is declared locally, like the other framework stand-ins in this
// fixture. This document derives `file_type: handler` from that interface.
//
// It deliberately contains no Publish/Subscribe *invocation*: MessagingDetector fires on any
// member-access call named Publish/PublishAsync/Subscribe/SubscribeAsync regardless of receiver,
// so a call here would emit a messaging half-edge and change the fixture's dependency graph.
// Implementing the interface and declaring HandleAsync proves the heuristic without doing that.

using Acme.Shared.Contracts;

namespace Acme.Orders.Events;

/// <summary>Stand-in for the integration-event handler contract.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>Reacts to an order this service published, updating local read state.</summary>
public sealed class OrderPlacedEventHandler : IIntegrationEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
