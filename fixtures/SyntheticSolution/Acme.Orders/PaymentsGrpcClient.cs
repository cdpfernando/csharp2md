// Stand-in for the gRPC client Acme.Orders uses to call Acme.Payments' Payments service.
//
// The fixture declares the Grpc.Core base type itself rather than referencing Grpc.AspNetCore, for
// the same reason IEventBus stands in for a message broker: the fixture must build and restore
// without pulling in an external dependency. GrpcClientDetector recognizes a generated client by
// its namespace-qualified base type (Grpc.Core.ClientBase), so this exercises the real detection
// rule rather than a weakened one.

namespace Grpc.Core
{
    /// <summary>Base type every generated gRPC client derives from.</summary>
    public abstract class ClientBase;
}

namespace Acme.Orders
{
    /// <summary>
    /// Shaped like protoc's generated client for the <c>Payments</c> service in
    /// <c>Acme.Payments/Protos/payments.proto</c>. <see cref="AuthorizePayment"/> is the unary RPC.
    /// </summary>
    public sealed class PaymentsClient : Grpc.Core.ClientBase
    {
        public Task<string> AuthorizePayment(string orderId, decimal amount) =>
            Task.FromResult($"{orderId}:{amount}");
    }
}
