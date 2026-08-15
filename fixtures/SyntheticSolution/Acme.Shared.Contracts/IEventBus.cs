namespace Acme.Shared.Contracts;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;

    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class;
}
