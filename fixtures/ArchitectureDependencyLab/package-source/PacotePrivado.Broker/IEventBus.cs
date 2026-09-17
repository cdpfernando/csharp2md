namespace PacotePrivado.Broker;

// SCENARIO:PKG-002
// SCENARIO:PKG-003
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
    Task SubscribeAsync<TEvent, THandler>(CancellationToken cancellationToken = default)
        where THandler : IIntegrationEventHandler<TEvent>;
}
