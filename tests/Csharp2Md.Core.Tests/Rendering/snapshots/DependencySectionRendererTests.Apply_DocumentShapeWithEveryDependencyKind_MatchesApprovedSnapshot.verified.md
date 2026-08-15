# Orders/OrderService.cs

Namespace: `Acme.Orders` | [Index](../index.md)

## Dependências detectadas

| Tipo de comunicação | Alvo | Resolução |
| --- | --- | --- |
| sincrono-bloqueante | PaymentService | hard-coded |
| streaming-bidirecional | Payments | dynamic |
| pub-sub-evento | OrderPlaced | unresolved |
| direct-reference | Acme.Shared.Contracts | not-applicable |

## Preamble

```csharp
// File header comment.
using System;
using System.Threading.Tasks;
```

## Namespace `Acme.Orders`

```csharp

namespace Acme.Orders
{
```

### `class OrderService`

**summary**: Places orders.

```csharp
    /// <summary>Places orders.</summary>
    public sealed class OrderService
    {
```

#### Field `_retries`

```csharp
        private readonly int _retries = 3;
```

#### Method `PlaceOrderAsync`

**summary**: Places an order.

**param** `id`: The order id.

```csharp

        /// <summary>Places an order.</summary>
        /// <param name="id">The order id.</param>
        public async Task PlaceOrderAsync(int id)
        {
            for (var i = 0; i < _retries; i++)
            {
                Console.WriteLine(id);
            }

            await Task.Delay(1);
        }
```

### End of `class OrderService`

```csharp
    }
```

## End of namespace `Acme.Orders`

```csharp
}
```

