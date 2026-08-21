// HTTP surface for Acme.Orders.
//
// Microsoft.AspNetCore.Mvc.ControllerBase is declared locally, like the other framework stand-ins
// in this fixture, so no ASP.NET Core reference is needed. This document exercises two rules at
// once: `file_type: controller` and the `api-endpoint` tag. It deliberately makes no outbound
// call — an IHttpClientFactory.CreateClient here would add a dependency edge and change
// dependencies.json under the v1 graph tests.

namespace Microsoft.AspNetCore.Mvc
{
    /// <summary>Stand-in for the base type every API controller derives from.</summary>
    public abstract class ControllerBase;
}

namespace Acme.Orders.Api
{
    using Microsoft.AspNetCore.Mvc;

    /// <summary>Read endpoints for orders. State-changing work lives in <see cref="OrderService"/>.</summary>
    public sealed class OrdersController : ControllerBase
    {
        private readonly Data.OrderDbContext _orders;

        public OrdersController(Data.OrderDbContext orders) => _orders = orders;

        public string GetOrderStatus(Guid orderId) =>
            _orders.Orders.Find(record => record.Id == orderId)?.Status.ToString() ?? "Unknown";
    }
}
