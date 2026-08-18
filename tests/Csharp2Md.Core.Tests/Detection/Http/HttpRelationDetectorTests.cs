using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Detection.Http;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Detection.Http;

public sealed class HttpRelationDetectorTests
{
    private const string Request = "http-request";
    private const string NamedClient = "http-named-client";
    private const string BaseAddress = "http-base-address";
    private const string Timeout = "http-timeout";
    private const string HeaderKind = "http-header";

    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Program.cs");

    public static TheoryData<string, string, bool, string?, string?, FactResolution?> Cases => new()
    {
        // FACT-45: named client creation alone.
        { "var client = factory.CreateClient(\"Payments\");", NamedClient, true, "client_name", "Payments", FactResolution.Exact },

        // FACT-45: named client chained directly into a request is one relation, not two.
        { "factory.CreateClient(\"Payments\").GetAsync(\"/orders\");", Request, true, "client", "named:Payments", FactResolution.Exact },
        { "factory.CreateClient(\"Payments\").GetAsync(\"/orders\");", Request, true, "route", "/orders", FactResolution.Exact },
        { "factory.CreateClient(\"Payments\").GetAsync(\"/orders\");", NamedClient, false, null, null, null },

        // FACT-45: typed client field, method + constant route.
        { "_http.GetAsync(\"https://payments.local/api\");", Request, true, "http_method", "GET", FactResolution.Exact },
        { "_http.GetAsync(\"https://payments.local/api\");", Request, true, "route", "https://payments.local/api", FactResolution.Exact },
        { "_http.GetAsync(\"https://payments.local/api\");", Request, true, "client", "typed:_http", FactResolution.Exact },

        // FACT-45: every recognized verb.
        { "_http.GetStringAsync(\"/x\");", Request, true, "http_method", "GET", FactResolution.Exact },
        { "_http.PostAsync(\"/x\", null!);", Request, true, "http_method", "POST", FactResolution.Exact },
        { "_http.PutAsync(\"/x\", null!);", Request, true, "http_method", "PUT", FactResolution.Exact },
        { "_http.DeleteAsync(\"/x\");", Request, true, "http_method", "DELETE", FactResolution.Exact },
        { "_http.PatchAsync(\"/x\", null!);", Request, true, "http_method", "PATCH", FactResolution.Exact },

        // FACT-45 + FACT-43-style edge case: an irreducible route stays partial.
        { "_http.GetAsync(GetUrl()); static string GetUrl() => \"/x\";", Request, true, "route_expression", "GetUrl()", FactResolution.Partial },

        // Non-request members never produce a relation.
        { "_http.Dispose();", Request, false, null, null, null },
        { "_http.CancelPendingRequests();", Request, false, null, null, null },

        // FACT-46-style lookalikes: a foreign factory/client type emits nothing.
        { "var client = otherFactory.CreateClient(\"Payments\");", NamedClient, false, null, null, null },
        { "var svc = new Contoso.Client(); svc.GetAsync(\"/x\");", Request, false, null, null, null },

        // FACT-45: base address, literal and irreducible.
        { "_http.BaseAddress = new Uri(\"https://payments.internal\");", BaseAddress, true, "base_url", "https://payments.internal", FactResolution.Exact },
        { "_http.BaseAddress = new Uri(GetBase()); static string GetBase() => \"https://x\";", BaseAddress, true, "base_url_expression", "GetBase()", FactResolution.Partial },

        // FACT-45: timeout.
        { "_http.Timeout = TimeSpan.FromSeconds(30);", Timeout, true, "timeout", "TimeSpan.FromSeconds(30)", FactResolution.Exact },

        // FACT-45: headers, constant and irreducible.
        { "_http.DefaultRequestHeaders.Add(\"X-Api-Key\", \"secret\");", HeaderKind, true, "header_name", "X-Api-Key", FactResolution.Exact },
        { "_http.DefaultRequestHeaders.Add(\"X-Api-Key\", \"secret\");", HeaderKind, true, "header_value", "secret", FactResolution.Exact },
        { "_http.DefaultRequestHeaders.Add(\"X-Api-Key\", GetKey()); static string GetKey() => \"s\";", HeaderKind, true, "header_value_expression", "GetKey()", FactResolution.Partial },
        { "var fake = new Contoso.FakeClient(); fake.DefaultRequestHeaders.Add(\"X\", \"Y\");", HeaderKind, false, null, null, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Integration")]
    public void Detect_UsesConfirmedHttpClientEvidence(
        string source,
        string relationKind,
        bool expected,
        string? detailKey,
        string? detailValue,
        FactResolution? resolution)
    {
        var facts = Detect(Context(source));
        var matches = facts.Where(fact => fact.RelationKind == relationKind).ToArray();

        if (!expected)
        {
            Assert.Empty(matches);
            return;
        }

        var fact = Assert.Single(matches, item => item.Details.Any(detail =>
            detail.Key == detailKey && detail.Value == detailValue));
        Assert.Equal(resolution, fact.Header.Resolution);
        Assert.Equal(RelationPartition.Http, fact.Partition);
        Assert.NotEmpty(fact.Header.Evidence);
        Assert.Contains(fact.Header.Provenance, static provenance =>
            provenance.DetectorId?.Value == "id1:detector;name=io.csharp2md.http"
            && provenance.DetectorVersion == "1.0.0");

        // FACT-47: a source observation names no proven remote target.
        Assert.Null(fact.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
    }

    /// <summary>FACT-45: repeated identical requests are never collapsed.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_RepeatedIdenticalRequests_KeepDistinctIdentities()
    {
        var facts = Detect(Context("_http.GetAsync(\"/x\"); _http.GetAsync(\"/x\");"))
            .Where(fact => fact.RelationKind == Request)
            .ToArray();

        Assert.Equal(2, facts.Length);
        Assert.Equal(2, facts.Select(static fact => fact.RelationId.Value).Distinct().Count());
    }

    /// <summary>FACT-35/FACT-46: without a semantic document nothing can be confirmed.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_WithoutSemanticDocument_EmitsNothing()
    {
        var context = Context("_http.GetAsync(\"/x\");") with { SemanticDocument = null };
        Assert.Empty(new DetectorHost(documentDetectors: [new HttpRelationDetector()]).DetectDocument(context).Facts);
    }

    private static RelationFact[] Detect(Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext context) =>
        new DetectorHost(documentDetectors: [new HttpRelationDetector()])
            .DetectDocument(context)
            .Facts
            .OfType<RelationFact>()
            .ToArray();

    private static Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext Context(string body)
    {
        var stubsTree = CSharpSyntaxTree.ParseText(FrameworkStubs, path: "FrameworkStubs.cs");
        var tree = CSharpSyntaxTree.ParseText(
            BodyUsings + "\n" + Preamble + "\n" + body,
            path: "Program.cs");
        var compilation = CSharpCompilation.Create(
            "HttpDetection",
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
        using System.Net.Http;
        """;

    private const string Preamble = """
        var _http = new HttpClient();
        Microsoft.Extensions.Http.IHttpClientFactory factory = new Microsoft.Extensions.Http.RealHttpClientFactory();
        Contoso.IHttpClientFactory otherFactory = new Contoso.FakeHttpClientFactory();
        """;

    private const string FrameworkStubs = """
        using System.Net.Http;

        namespace Microsoft.Extensions.Http
        {
            public interface IHttpClientFactory
            {
                HttpClient CreateClient(string name);
            }

            public sealed class RealHttpClientFactory : IHttpClientFactory
            {
                public HttpClient CreateClient(string name) => new();
            }
        }

        namespace Contoso
        {
            public sealed class Client
            {
                public string GetAsync(string uri) => uri;
            }

            public sealed class FakeHeaders
            {
                public void Add(string name, string value) { }
            }

            public sealed class FakeClient
            {
                public FakeHeaders DefaultRequestHeaders { get; } = new();
            }

            public interface IHttpClientFactory
            {
                HttpClient CreateClient(string name);
            }

            public sealed class FakeHttpClientFactory : IHttpClientFactory
            {
                public HttpClient CreateClient(string name) => new();
            }
        }
        """;
}
