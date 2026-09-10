// GCPC-087, GCPC-090: reproduces the audit's contract-accounting gap (finding I2) in a versioned,
// committed fixture: a published event with a handler in the same solution, a published event with
// no handler, and two same-named payload types in unrelated projects (this one, and
// Certification.Api/DuplicatePayloadName.cs) that must never be merged into a shared contract by
// name alone.
//
// IEventBus and IIntegrationEventHandler<TEvent> are declared locally, like the equivalent stand-ins
// in fixtures/SyntheticSolution/Acme.Shared.Contracts.

namespace Certification.Messaging
{
    public interface IEventBus
    {
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class;
    }

    public interface IIntegrationEventHandler<in TEvent>
        where TEvent : class
    {
        Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
    }

    /// <summary>Published and handled in this same solution.</summary>
    public sealed record OrderCreated(Guid OrderId, DateTimeOffset CreatedAtUtc);

    /// <summary>Published with deliberately no handler anywhere in the corpus (unpaired-publish path).</summary>
    public sealed record OrderShipped(Guid OrderId, DateTimeOffset ShippedAtUtc);

    public sealed class OrderPublisher(IEventBus eventBus)
    {
        public Task PublishOrderCreatedAsync(Guid orderId, CancellationToken cancellationToken) =>
            eventBus.PublishAsync(new OrderCreated(orderId, DateTimeOffset.UtcNow), cancellationToken);

        public Task PublishOrderShippedAsync(Guid orderId, CancellationToken cancellationToken) =>
            eventBus.PublishAsync(new OrderShipped(orderId, DateTimeOffset.UtcNow), cancellationToken);
    }

    /// <summary>Reacts to <see cref="OrderCreated"/>, the paired handler for the contract case.</summary>
    public sealed class OrderCreatedEventHandler : IIntegrationEventHandler<OrderCreated>
    {
        public Task HandleAsync(OrderCreated integrationEvent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(integrationEvent);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Same type name as <c>Certification.Api.DuplicatePayloadName.Receipt</c>, deliberately unrelated
    /// (no project reference either way) so GCPC-090 has a same-named, distinct-identity pair to prove
    /// no shared contract is ever created from name similarity alone.
    /// </summary>
    public sealed record Receipt(Guid OrderId, decimal Total);
}
