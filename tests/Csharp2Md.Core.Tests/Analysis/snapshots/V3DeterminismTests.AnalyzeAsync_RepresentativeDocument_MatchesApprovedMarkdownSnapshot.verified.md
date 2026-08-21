---
schema_version: 2
document_id: "id1:document;project=id1%3Aproject%3Bpath%3DAcme.Orders%252FAcme.Orders.csproj;path=Acme.Orders%2FOrderService.cs"
project_id: "id1:project;path=Acme.Orders%2FAcme.Orders.csproj"
component_ids: []
classifications: []
analysis_summary:
  resolution: syntactic
  symbol_count: 7
  relation_count: 0
  diagnostic_count: 0
diagnostics: []
facts_ref: "facts/document/45/4532df7bd67a908fab160445df45ff9201f190a3d382cadfce97b89a521a185a.json"
---

# Acme.Orders/OrderService.cs

## Analysis

```yaml
resolution: syntactic
symbols:
  syntactic: 7
relations: {}
diagnostics: {}
```

## Preamble

```csharp
using System.Net.Http.Json;
using Acme.Shared.Contracts;
using Microsoft.Extensions.Http;
```

## Namespace

```csharp

namespace Acme.Orders;
```

## Class

```csharp

public sealed class OrderService(
    IHttpClientFactory httpClientFactory, IEventBus eventBus, PaymentsClient paymentsClient, PaymentClient crossProjectPaymentClient)
{
```

## Method

```csharp
    /// <summary>Unary gRPC call into Acme.Payments' Payments service.</summary>
    public Task<string> AuthorizePaymentAsync(Guid orderId, decimal amount) =>
        paymentsClient.AuthorizePayment(orderId.ToString(), amount);
```

## Method

```csharp

    /// <summary>
    /// RELR-04/T29 (spec.md's first P1 Independent Test): calls PaymentClient.Authorize (declared in
    /// Acme.Shared.Contracts, a different project from OrderService's own Acme.Orders) through a
    /// primary-constructor-parameter receiver.
    /// </summary>
    public Task<string> AuthorizeViaPaymentClientAsync(Guid orderId, decimal amount) =>
        crossProjectPaymentClient.Authorize(orderId, amount);
```

## Method

```csharp

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
```

## Method

```csharp

    /// <summary>"NotificationService" resolves via an env-var indirection in appsettings.json (dynamic).</summary>
    public async Task NotifyOrderPlacedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var notificationClient = httpClientFactory.CreateClient("NotificationService");
        await notificationClient.PostAsJsonAsync("notifications/order-placed", new { orderId }, cancellationToken);
    }
```

## Method

```csharp

    /// <summary>"ShippingService" has no entry anywhere in appsettings.json (unresolved).</summary>
    public async Task RequestShippingAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var shippingClient = httpClientFactory.CreateClient("ShippingService");
        await shippingClient.PostAsJsonAsync("shipments", new { orderId }, cancellationToken);
    }
```

## End of class

```csharp
}
```

