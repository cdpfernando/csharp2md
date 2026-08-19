using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Detection.AspNetCore;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Detection.AspNetCore;

public sealed class AspNetCoreDetectorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Program.cs");

    public static TheoryData<string, string, bool, string?, string?, FactResolution?> Cases => new()
    {
        { "public class Orders : ControllerBase { }", "aspnet-controller", true, "controller", "Orders", FactResolution.Exact },
        { "[ApiController] public class Orders { }", "aspnet-controller", true, null, null, FactResolution.Exact },
        { "public class Orders : ControllerBase { [HttpGet(\"/orders\")] public string Get() => \"ok\"; }", "http-endpoint", true, "route", "/orders", FactResolution.Exact },
        { "public class Orders : ControllerBase { [HttpPost(\"/orders\")] public string Post() => \"ok\"; }", "http-endpoint", true, "http_method", "POST", FactResolution.Exact },
        { "public class Orders : ControllerBase { [HttpPut(\"/orders\")] public string Put() => \"ok\"; }", "http-endpoint", true, "http_method", "PUT", FactResolution.Exact },
        { "public class Orders : ControllerBase { [HttpDelete(\"/orders\")] public string Delete() => \"ok\"; }", "http-endpoint", true, "http_method", "DELETE", FactResolution.Exact },
        { "public class Orders : ControllerBase { [HttpPatch(\"/orders\")] public string Patch() => \"ok\"; }", "http-endpoint", true, "http_method", "PATCH", FactResolution.Exact },
        { "public class Orders : ControllerBase { [HttpHead(\"/orders\")] public string Head() => \"ok\"; }", "http-endpoint", true, "http_method", "HEAD", FactResolution.Exact },
        { "public class Orders : ControllerBase { [HttpOptions(\"/orders\")] public string Options() => \"ok\"; }", "http-endpoint", true, "http_method", "OPTIONS", FactResolution.Exact },
        { "public class Orders : ControllerBase { [NonAction] public string Get() => \"ok\"; }", "http-endpoint", false, null, null, null },
        { "public class OrdersController { public string Get() => \"ok\"; }", "aspnet-controller", false, null, null, null },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapGet(\"/orders\", () => \"ok\");", "http-endpoint", true, "http_method", "GET", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapPost(\"/orders\", () => \"ok\");", "http-endpoint", true, "http_method", "POST", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapPut(\"/orders\", () => \"ok\");", "http-endpoint", true, "http_method", "PUT", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapDelete(\"/orders\", () => \"ok\");", "http-endpoint", true, "http_method", "DELETE", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapPatch(\"/orders\", () => \"ok\");", "http-endpoint", true, "http_method", "PATCH", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapMethods(\"/orders\", new[] { \"GET\" }, () => \"ok\");", "http-endpoint", true, "http_method", "MULTIPLE", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.Map(\"/orders\", () => \"ok\");", "http-endpoint", true, "http_method", "ANY", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); var route = GetRoute(); app.MapGet(route, () => \"ok\"); string GetRoute() => \"/orders\";", "http-endpoint", true, "route_expression", "route", FactResolution.Partial },
        { "var app = new Contoso.App(); app.MapGet(\"/orders\", () => \"ok\");", "http-endpoint", false, null, null, null },
        { "[Authorize(Policy = \"admin\")] public class Orders : ControllerBase { }", "aspnet-authorization", true, "policy", "admin", FactResolution.Exact },
        { "public class Orders : ControllerBase { [Authorize(Policy = \"admin\")] public string Get() => \"ok\"; }", "aspnet-authorization", true, "policy", "admin", FactResolution.Exact },
        { "[AllowAnonymous] public class Orders : ControllerBase { }", "aspnet-authorization", true, "policy", "allow-anonymous", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapGet(\"/\", () => \"ok\").RequireAuthorization(\"admin\");", "aspnet-authorization", true, "policy", "admin", FactResolution.Exact },
        { "var options = new AuthorizationOptions(); options.AddPolicy(\"admin\", new object());", "aspnet-policy", true, "policy", "admin", FactResolution.Exact },
        { "[AuditFilter] public class Orders : ControllerBase { }", "aspnet-filter", true, "filter", "AuditFilterAttribute", FactResolution.Exact },
        { "public class Orders : ControllerBase { [AuditFilter] public string Get() => \"ok\"; }", "aspnet-filter", true, "filter", "AuditFilterAttribute", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapGet(\"/\", () => \"ok\").AddEndpointFilter(new AuditFilter());", "aspnet-filter", true, "filter", "AuditFilter", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.MapHealthChecks(\"/health\");", "aspnet-health-check", true, "route", "/health", FactResolution.Exact },
        { "var app = WebApplication.CreateBuilder(args).Build(); app.Run();", "aspnet-entrypoint", true, "operation", "CreateBuilder", FactResolution.Exact },
        { "var app = new Contoso.App(); app.MapHealthChecks(\"/health\");", "aspnet-health-check", false, null, null, null },
        { "[Contoso.Authorize(Policy = \"admin\")] public class Orders : ControllerBase { }", "aspnet-authorization", false, null, null, null },
        { "public class Orders : ControllerBase { [HttpGet(RouteNames.Orders)] public string Get() => \"ok\"; }", "http-endpoint", true, "route", null, FactResolution.Exact },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Integration")]
    public void Detect_UsesConfirmedFrameworkEvidence(
        string source,
        string relationKind,
        bool expected,
        string? detailKey,
        string? detailValue,
        FactResolution? resolution)
    {
        var context = Context(source);
        var result = new DetectorHost(documentDetectors: [new AspNetCoreDetector()]).DetectDocument(context);
        var matches = result.Facts.OfType<RelationFact>()
            .Where(fact => fact.RelationKind == relationKind)
            .ToArray();

        if (!expected)
        {
            Assert.Empty(matches);
            return;
        }

        var fact = detailKey is null
            ? Assert.Single(matches)
            : Assert.Single(matches, item => item.Details.Any(detail =>
                detail.Key == detailKey && (detailValue is null || detail.Value == detailValue)));
        Assert.Equal(resolution, fact.Header.Resolution);
        Assert.NotEmpty(fact.Header.Evidence);
        Assert.Contains(fact.Header.Provenance, static provenance =>
            provenance.DetectorId?.Value == "id1:detector;name=io.csharp2md.aspnet-core"
            && provenance.DetectorVersion == "1.0.0");
        Assert.Null(fact.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
    }

    private static Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext Context(string body)
    {
        var stubsTree = CSharpSyntaxTree.ParseText(FrameworkStubs, path: "FrameworkStubs.cs");
        var tree = CSharpSyntaxTree.ParseText(BodyUsings + "\n" + body, path: "Program.cs");
        var compilation = CSharpCompilation.Create(
            "AspNetDetection",
            [stubsTree, tree],
            TestCompilation.PlatformReferences,
            new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.ConsoleApplication));
        var model = compilation.GetSemanticModel(tree);
        var project = new ProjectFact(
            Header(ProjectId.ToFactId(), FactKind.Project),
            ProjectId,
            "App",
            "src/App/App.csproj",
            [TargetId],
            [DocumentId]);
        var target = new TargetFact(
            Header(TargetId.ToFactId(), FactKind.Target),
            TargetId,
            ProjectId,
            "net10.0");
        var document = new DocumentFact(
            Header(DocumentId.ToFactId(), FactKind.Document),
            DocumentId,
            ProjectId,
            "Program.cs",
            [],
            []);
        var index = SolutionAnalysisIndex.Build(
            [project],
            [new TargetAnalysisIndexInput(target, [], [])]);
        return new Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext(
            project,
            target,
            document,
            [],
            index,
            new SemanticDetectionDocument(tree, model));
    }

    private static FactHeader Header(FactId id, FactKind kind) =>
        FactHeader.Create(id, kind, FactResolution.Exact);

    private const string BodyUsings = """
        using System;
        using Microsoft.AspNetCore.Authorization;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Diagnostics.HealthChecks;
        using Microsoft.AspNetCore.Mvc;
        """;

    private const string FrameworkStubs = """
        using System;
        using Microsoft.AspNetCore.Authorization;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Diagnostics.HealthChecks;
        using Microsoft.AspNetCore.Mvc;

        namespace Microsoft.AspNetCore.Mvc
        {
            public class ControllerBase { }
            public class Controller : ControllerBase { }
            public sealed class ApiControllerAttribute : Attribute { }
            public sealed class NonActionAttribute : Attribute { }
            public class RouteAttribute(string template) : Attribute { }
            public class HttpGetAttribute(string? template = null) : Attribute { }
            public class HttpPostAttribute(string? template = null) : Attribute { }
            public class HttpPutAttribute(string? template = null) : Attribute { }
            public class HttpDeleteAttribute(string? template = null) : Attribute { }
            public class HttpPatchAttribute(string? template = null) : Attribute { }
            public class HttpHeadAttribute(string? template = null) : Attribute { }
            public class HttpOptionsAttribute(string? template = null) : Attribute { }
        }

        namespace Microsoft.AspNetCore.Authorization
        {
            public sealed class AuthorizeAttribute : Attribute { public string? Policy { get; set; } }
            public sealed class AllowAnonymousAttribute : Attribute { }
            public sealed class AuthorizationOptions { public void AddPolicy(string name, object policy) { } }
        }

        namespace Microsoft.AspNetCore.Mvc.Filters
        {
            public interface IFilterMetadata { }
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public sealed class WebApplicationBuilder { public WebApplication Build() => new(); }
            public sealed class WebApplication
            {
                public static WebApplicationBuilder CreateBuilder(string[] args) => new();
                public void Run() { }
            }
            public sealed class RouteHandlerBuilder { }
            public static class EndpointRouteBuilderExtensions
            {
                public static RouteHandlerBuilder MapGet(this WebApplication app, string pattern, Func<string> handler) => new();
                public static RouteHandlerBuilder MapPost(this WebApplication app, string pattern, Func<string> handler) => new();
                public static RouteHandlerBuilder MapPut(this WebApplication app, string pattern, Func<string> handler) => new();
                public static RouteHandlerBuilder MapDelete(this WebApplication app, string pattern, Func<string> handler) => new();
                public static RouteHandlerBuilder MapPatch(this WebApplication app, string pattern, Func<string> handler) => new();
                public static RouteHandlerBuilder MapMethods(this WebApplication app, string pattern, string[] methods, Func<string> handler) => new();
                public static RouteHandlerBuilder Map(this WebApplication app, string pattern, Func<string> handler) => new();
                public static RouteHandlerBuilder RequireAuthorization(this RouteHandlerBuilder builder, params string[] policyNames) => builder;
                public static RouteHandlerBuilder AddEndpointFilter<T>(this RouteHandlerBuilder builder, T filter) => builder;
            }
        }

        namespace Microsoft.AspNetCore.Diagnostics.HealthChecks
        {
            public static class HealthCheckEndpointRouteBuilderExtensions
            {
                public static Microsoft.AspNetCore.Builder.RouteHandlerBuilder MapHealthChecks(
                    this Microsoft.AspNetCore.Builder.WebApplication app,
                    string pattern) => new();
            }
        }

        public sealed class AuditFilter { }
        public sealed class AuditFilterAttribute : Attribute, Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata { }
        public static class RouteNames { public const string Orders = "/orders"; }

        namespace Contoso
        {
            public sealed class App { }
            public static class LookalikeExtensions
            {
                public static void MapGet(this App app, string pattern, Func<string> handler) { }
                public static void MapHealthChecks(this App app, string pattern) { }
            }
            public sealed class AuthorizeAttribute : Attribute { public string? Policy { get; set; } }
        }
        """;
}
