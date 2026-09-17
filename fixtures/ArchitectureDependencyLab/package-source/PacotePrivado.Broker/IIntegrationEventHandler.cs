namespace PacotePrivado.Broker;

// SCENARIO:PKG-002
// SCENARIO:PKG-003
public interface IIntegrationEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
