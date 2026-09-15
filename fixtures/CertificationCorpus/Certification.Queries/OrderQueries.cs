// GCPC-018: the single concrete implementation of Certification.Api.IOrderQueries, deliberately
// declared in a different project from the interface and its caller (Certification.Api), matching
// the audit's real IOrderQueries / OrderQueries cross-project shape.

using Certification.Api;

namespace Certification.Queries;

public sealed class OrderQueries : IOrderQueries
{
    public string? GetOrderStatus(Guid orderId) => orderId == Guid.Empty ? null : "Placed";
}
