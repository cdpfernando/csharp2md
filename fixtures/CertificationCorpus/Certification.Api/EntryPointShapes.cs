// GCPC-025: reproduces the audit's private-helper entry-point regression
// (CatalogController.ChangeUriPlaceholder) in a versioned, committed fixture, so the classifier
// fix in a later phase has a corpus-resident case to invert without requiring the local eShop clone.
//
// Microsoft.AspNetCore.Mvc.ControllerBase and HttpGetAttribute are declared locally, like the other
// framework stand-ins in fixtures/SyntheticSolution, so no ASP.NET Core reference is needed.
// ControllerBase is declared `partial` because Certification.Api/InvocationChain.cs (T3) adds the
// Ok/NotFound stand-ins to the same type in a second file.

namespace Microsoft.AspNetCore.Mvc
{
    /// <summary>Stand-in for the base type every API controller derives from.</summary>
    public abstract partial class ControllerBase;

    /// <summary>Stand-in for HTTP GET route attributes.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HttpGetAttribute(string template) : Attribute
    {
        public string Template { get; } = template;
    }
}

namespace Certification.Api
{
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Carries, in one type, all three shapes the audit's entry-point regression turns on: a
    /// routed action, a conventional action with no route attribute, and a private helper equivalent
    /// to <c>CatalogController.ChangeUriPlaceholder</c>.
    /// </summary>
    public sealed class WidgetsController : ControllerBase
    {
        /// <summary>Public, routed action. Proven entry capability by an explicit route.</summary>
        [HttpGet("widgets/{id}")]
        public string GetWidget(Guid id) => ChangeUriPlaceholder(id.ToString());

        /// <summary>Public, conventional action with no route attribute (EBC-08 legitimate case).</summary>
        public string Index() => "widgets-index";

        /// <summary>
        /// Private helper on a controller type. The pre-fix classifier promotes every callable
        /// declared on a <see cref="ControllerBase"/> descendant with no accessibility check, so this
        /// currently becomes a fabricated `EntryPoint` fact exactly like `ChangeUriPlaceholder` did.
        /// </summary>
        private static string ChangeUriPlaceholder(string uriTemplate) =>
            uriTemplate.Replace("{id}", "0", StringComparison.Ordinal);
    }
}
