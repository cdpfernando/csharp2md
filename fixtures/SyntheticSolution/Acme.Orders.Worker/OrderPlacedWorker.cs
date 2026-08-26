// This file exists so Acme.Orders.Worker is a second application in Acme.Orders.slnx.
// Two applications there make Acme.Shared.Contracts a shared component; the same
// library stays privately used in Acme.Payments.slnx. That is the 5D grouping ground truth.
//
// Main consumes IEventBus by subscribing a handler for OrderPlaced. The worker declares
// no HTTP client, no DbContext, and no route, so it adds no boundary, contract, or
// persistence facts.

using Acme.Shared.Contracts;

namespace Acme.Orders.Worker;

public static class OrderPlacedWorker
{
    public static void Main(string[] args)
    {
        _ = args;
        new OrderPlacedHandler(NullEventBus.Instance).Start();
    }

    private sealed class OrderPlacedHandler
    {
        private readonly IEventBus _eventBus;

        public OrderPlacedHandler(IEventBus eventBus)
        {
            ArgumentNullException.ThrowIfNull(eventBus);
            _eventBus = eventBus;
        }

        public void Start() => _eventBus.Subscribe<OrderPlaced>(HandleAsync);

        private static Task HandleAsync(OrderPlaced @event, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(@event);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class NullEventBus : IEventBus
    {
        public static readonly NullEventBus Instance = new();

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class =>
            Task.CompletedTask;

        public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : class =>
            _ = handler;
    }
}
