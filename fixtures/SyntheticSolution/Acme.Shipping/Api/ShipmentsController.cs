// HTTP surface for Acme.Shipping.
//
// Microsoft.AspNetCore.Mvc types are declared locally, like OrdersController, so no ASP.NET Core
// reference is needed. HttpPost is the stand-in Orders does not declare; it closes the
// ShippingService client call OrderService already makes (PostAsJsonAsync("shipments", ...)).

namespace Microsoft.AspNetCore.Mvc
{
    /// <summary>Stand-in for the base type every API controller derives from.</summary>
    public abstract class ControllerBase;

    /// <summary>Stand-in for HTTP POST route attributes.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HttpPostAttribute(string template) : Attribute
    {
        public string Template { get; } = template;
    }
}

namespace Acme.Shipping.Api
{
    using Microsoft.AspNetCore.Mvc;

    /// <summary>Write endpoint for shipments requested by Acme.Orders.</summary>
    public sealed class ShipmentsController : ControllerBase
    {
        [HttpPost("shipments")]
        public string CreateShipment(Guid orderId) => orderId.ToString();
    }
}
