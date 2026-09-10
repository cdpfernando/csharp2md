// GCPC-018: reproduces the audit's dropped-invocation regression
// (OrdersController.GetOrderAsync -> IOrderQueries.GetOrderAsync -> OrderQueries.GetOrderAsync) in a
// versioned, committed fixture: a controller action calls an injected interface method whose single
// concrete implementation lives in another project of the same solution
// (Certification.Queries, see OrderQueries.cs), plus two framework calls equivalent to the audit's
// `Ok` and `NotFound`. The classifier fix for the interface-dispatch disposition lands in a later
// phase (T23); this task only proves the shape is present and reproducible in CI.
//
// The Ok/NotFound stand-ins extend `Microsoft.AspNetCore.Mvc.ControllerBase` declared as `partial` in
// EntryPointShapes.cs (T2), matching InvokesPass.IsFrameworkSignature's `global::Microsoft.` container
// check exactly, like the other framework stand-ins in this fixture family.

namespace Microsoft.AspNetCore.Mvc
{
    public abstract partial class ControllerBase
    {
        protected OkResult Ok(object? value) => new(value);

        protected NotFoundResult NotFound() => new();
    }

    public sealed class OkResult(object? value)
    {
        public object? Value { get; } = value;
    }

    public sealed class NotFoundResult;
}

namespace Certification.Api
{
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Declared here (not in Certification.Queries) so the caller needs no reference to the project
    /// that implements it — the concrete implementation project references this one instead, matching
    /// the audit's real dependency direction.
    /// </summary>
    public interface IOrderQueries
    {
        string? GetOrderStatus(Guid orderId);
    }

    public sealed class OrderQueriesController : ControllerBase
    {
        private readonly IOrderQueries _queries;

        public OrderQueriesController(IOrderQueries queries)
        {
            _queries = queries;
        }

        [HttpGet("orders/{id}/status")]
        public object GetOrderStatus(Guid id)
        {
            var status = _queries.GetOrderStatus(id);
            if (status is null)
            {
                return NotFound();
            }

            return Ok(status);
        }
    }
}
