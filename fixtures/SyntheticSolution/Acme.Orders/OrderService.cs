using System.Net.Http.Json;
using Acme.Shared.Contracts;
using Microsoft.Extensions.Http;

namespace Acme.Orders;

public sealed class OrderService(IHttpClientFactory httpClientFactory, IEventBus eventBus)
{
    public async Task PlaceOrderAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        var paymentClient = httpClientFactory.CreateClient("PaymentService");
        var response = await paymentClient.PostAsJsonAsync(
            "payments/authorize",
            new { orderId, amount },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await eventBus.PublishAsync(new OrderPlaced(orderId, amount, DateTimeOffset.UtcNow), cancellationToken);
    }

    /// <summary>"NotificationService" resolves via an env-var indirection in appsettings.json (dynamic).</summary>
    public async Task NotifyOrderPlacedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var notificationClient = httpClientFactory.CreateClient("NotificationService");
        await notificationClient.PostAsJsonAsync("notifications/order-placed", new { orderId }, cancellationToken);
    }

    /// <summary>"ShippingService" has no entry anywhere in appsettings.json (unresolved).</summary>
    public async Task RequestShippingAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var shippingClient = httpClientFactory.CreateClient("ShippingService");
        await shippingClient.PostAsJsonAsync("shipments", new { orderId }, cancellationToken);
    }
}
