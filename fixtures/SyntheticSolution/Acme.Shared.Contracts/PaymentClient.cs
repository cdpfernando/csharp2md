namespace Acme.Shared.Contracts;

/// <summary>
/// RELR-04/T29 (spec.md's first P1 Independent Test): a real, always-resolvable type OrderService
/// calls across a project boundary through several receiver shapes. Deliberately not co-located with
/// <c>Acme.Payments</c> itself: Acme.Payments carries a Grpc.AspNetCore package reference, and a
/// ProjectReference from Acme.Orders would pull that in transitively, colliding with this fixture's own
/// deliberate ASP.NET Core/Grpc stand-in types (WebApplicationBuilder, ControllerBase, ClientBase - see
/// PaymentsGrpcClient.cs and Program.cs). Acme.Shared.Contracts has no package dependencies and is
/// already referenced by every service project, so it is the only project that can host a new
/// cross-project symbol without disturbing the fixture's existing build-isolation design. Unlike
/// <c>PaymentsClient</c> (the gRPC stand-in co-located with its own consumer in Acme.Orders), this type
/// exists specifically to exercise resolution across a real project boundary.
/// </summary>
public sealed class PaymentClient
{
    public Task<string> Authorize(Guid orderId, decimal amount) => Task.FromResult($"{orderId}:{amount}");
}
