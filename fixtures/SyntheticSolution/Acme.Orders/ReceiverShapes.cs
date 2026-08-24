using Acme.Shared.Contracts;

namespace Acme.Orders;

/// <summary>
/// RELR-04/T29: one call site per receiver shape <c>DeclaredReceiverTypeName</c> resolves (T7-T10),
/// each targeting the same real, always-resolvable <see cref="PaymentClient.Authorize"/> so a shape's
/// resolution can be told apart only by how the receiver was declared, never by what it targets.
/// <see cref="OrderService"/> already covers the primary-constructor-parameter shape; the fixture's
/// existing var-typed local (<c>OrderService.PlaceOrderAsync</c>'s <c>response</c>) covers that one.
/// </summary>
public sealed class ReceiverShapes
{
    private readonly PaymentClient _fieldClient;

    private PaymentClient PropertyClient { get; }

    public ReceiverShapes(PaymentClient constructorClient)
    {
        _fieldClient = constructorClient;
        PropertyClient = constructorClient;
        // Constructor-parameter receiver: constructorClient is a plain (non-primary) constructor
        // parameter, not yet captured as a field or property when this call is made.
        _ = constructorClient.Authorize(Guid.Empty, 0m);
    }

    public Task<string> ViaField() => _fieldClient.Authorize(Guid.Empty, 0m);

    public Task<string> ViaProperty() => PropertyClient.Authorize(Guid.Empty, 0m);

    public Task<string> ViaPatternVariable(object candidate)
    {
        if (candidate is PaymentClient matched)
        {
            return matched.Authorize(Guid.Empty, 0m);
        }

        return Task.FromResult(string.Empty);
    }
}
