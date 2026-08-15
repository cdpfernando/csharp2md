namespace Acme.Shared.Contracts;

/// <summary>Published by Acme.Orders when an order is placed. Subscribed to by Acme.Payments.</summary>
public sealed record OrderPlaced(Guid OrderId, decimal Amount, DateTimeOffset PlacedAtUtc);

/// <summary>Published by Acme.Payments after a payment is processed. Deliberately has no subscriber
/// in this fixture, so it exercises the unpaired-publish path (P2-15).</summary>
public sealed record PaymentProcessed(Guid OrderId, Guid PaymentId, DateTimeOffset ProcessedAtUtc);
