// This file exists to make the project an application for the 5D grouping rule.
// Main exercises the existing PaymentsService surface and adds no new gRPC method,
// contract, or boundary operation.

using Acme.Shared.Contracts;

namespace Acme.Payments;

public static class Program
{
    public static void Main(string[] args)
    {
        _ = args;
        _ = new PaymentsService(NullEventBus.Instance);
    }

    private sealed class NullEventBus : IEventBus
    {
        public static readonly NullEventBus Instance = new();

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class =>
            Task.CompletedTask;

        public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : class
        {
            _ = handler;
        }
    }
}
