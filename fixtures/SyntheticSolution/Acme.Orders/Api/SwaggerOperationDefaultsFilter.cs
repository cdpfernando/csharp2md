// OpenAPI document customization for Acme.Orders.
//
// Swashbuckle's IOperationFilter is declared locally, like the other framework stand-ins in this
// fixture. This document derives `file_type: filter` from that interface — the rule that would
// otherwise have no exercise anywhere in the repository.

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    /// <summary>Stand-in for one generated OpenAPI operation.</summary>
    public sealed class OpenApiOperation
    {
        public IList<string> Tags { get; } = [];
    }

    /// <summary>Stand-in for the per-operation customization hook.</summary>
    public interface IOperationFilter
    {
        void Apply(OpenApiOperation operation);
    }
}

namespace Acme.Orders.Api
{
    using Swashbuckle.AspNetCore.SwaggerGen;

    /// <summary>Tags every generated operation with the owning service.</summary>
    public sealed class SwaggerOperationDefaultsFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            operation.Tags.Add("Acme.Orders");
        }
    }
}
