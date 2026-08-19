namespace Acme.Shared.Contracts;

/// <summary>Lifecycle an order moves through. The fixture's only enum declaration.</summary>
public enum OrderStatus
{
    Pending,
    Authorized,
    Shipped,
}
