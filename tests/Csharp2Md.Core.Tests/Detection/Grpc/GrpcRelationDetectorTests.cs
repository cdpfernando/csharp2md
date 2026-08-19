using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Detection;
using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Detection.Grpc;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Detection.Grpc;

public sealed class GrpcRelationDetectorTests
{
    private const string Call = "grpc-call";

    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Program.cs");

    public static TheoryData<string, bool, string?, string?, FactResolution?> Cases => new()
    {
        // FACT-46: unary async call, confirmed by the AsyncUnaryCall<T> return type.
        { "var call = _payments.ChargeAsync(_request);", true, "call_shape", "unary", FactResolution.Exact },
        { "var call = _payments.ChargeAsync(_request);", true, "service", "Payments", FactResolution.Exact },
        { "var call = _payments.ChargeAsync(_request);", true, "method", "ChargeAsync", FactResolution.Exact },

        // FACT-46: a blocking overload returning the response type directly is still unary.
        { "var reply = _payments.Charge(_request);", true, "call_shape", "unary", FactResolution.Exact },

        // FACT-46: server streaming and duplex streaming are both "streaming".
        { "var call = _payments.Watch(_request);", true, "call_shape", "streaming", FactResolution.Exact },
        { "var call = _payments.Exchange();", true, "call_shape", "streaming", FactResolution.Exact },
        { "var call = _payments.Upload();", true, "call_shape", "streaming", FactResolution.Exact },

        // A generated client whose type name does not end in "Client" keeps its full name.
        { "var call = _legacy.ChargeAsync(_request);", true, "service", "PaymentsGateway", FactResolution.Exact },

        // Client configuration calls issue no RPC.
        { "var configured = _payments.WithHost(\"localhost\");", false, null, null, null },

        // A call on something that is not a generated gRPC client produces no relation.
        { "var text = _request.ToString();", false, null, null, null },

        // FACT-46: a name-only lookalike (its own unrelated ClientBase, not Grpc.Core's) emits nothing.
        { "var call = _lookalike.ChargeAsync(_request);", false, null, null, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Integration")]
    public void Detect_UsesConfirmedGeneratedClientEvidence(
        string source,
        bool expected,
        string? detailKey,
        string? detailValue,
        FactResolution? resolution)
    {
        var facts = Detect(Context(source));
        var matches = facts.Where(fact => fact.RelationKind == Call).ToArray();

        if (!expected)
        {
            Assert.Empty(matches);
            return;
        }

        var fact = Assert.Single(matches, item => item.Details.Any(detail =>
            detail.Key == detailKey && detail.Value == detailValue));
        Assert.Equal(resolution, fact.Header.Resolution);
        Assert.Equal(RelationPartition.Grpc, fact.Partition);
        Assert.NotEmpty(fact.Header.Evidence);
        Assert.Contains(fact.Header.Provenance, static provenance =>
            provenance.DetectorId?.Value == "id1:detector;name=io.csharp2md.grpc"
            && provenance.DetectorVersion == "1.0.0");

        // FACT-47: a generated-client source observation names no proven remote target.
        Assert.Null(fact.TargetId);
        Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
    }

    /// <summary>FACT-35/FACT-46: without a semantic document nothing can be confirmed.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Detect_WithoutSemanticDocument_EmitsNothing()
    {
        var context = Context("var call = _payments.ChargeAsync(_request);") with { SemanticDocument = null };
        Assert.Empty(new DetectorHost(documentDetectors: [new GrpcRelationDetector()]).DetectDocument(context).Facts);
    }

    private static RelationFact[] Detect(Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext context) =>
        new DetectorHost(documentDetectors: [new GrpcRelationDetector()])
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
            "GrpcDetection",
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
        using Acme.Contracts;
        """;

    private const string Preamble = """
        var _payments = new Payments.PaymentsClient();
        var _legacy = new Payments.PaymentsGateway();
        var _lookalike = new Contoso.PaymentsClient();
        var _request = new ChargeRequest();
        """;

    private const string FrameworkStubs = """
        namespace Grpc.Core
        {
            public class ClientBase { }
            public class ClientBase<T> : ClientBase { }
            public class AsyncUnaryCall<T> { }
            public class AsyncServerStreamingCall<T> { }
            public class AsyncClientStreamingCall<TRequest, TResponse> { }
            public class AsyncDuplexStreamingCall<TRequest, TResponse> { }
        }

        namespace Acme.Contracts
        {
            public class ChargeRequest { }

            public class PaymentReply { }

            public class Payments
            {
                public class PaymentsClient : global::Grpc.Core.ClientBase<PaymentsClient>
                {
                    public PaymentReply Charge(ChargeRequest request) => null!;

                    public global::Grpc.Core.AsyncUnaryCall<PaymentReply> ChargeAsync(ChargeRequest request) => null!;

                    public global::Grpc.Core.AsyncServerStreamingCall<PaymentReply> Watch(ChargeRequest request) => null!;

                    public global::Grpc.Core.AsyncDuplexStreamingCall<ChargeRequest, PaymentReply> Exchange() => null!;

                    public global::Grpc.Core.AsyncClientStreamingCall<ChargeRequest, PaymentReply> Upload() => null!;

                    public PaymentsClient WithHost(string host) => this;
                }

                public class PaymentsGateway : global::Grpc.Core.ClientBase<PaymentsGateway>
                {
                    public global::Grpc.Core.AsyncUnaryCall<PaymentReply> ChargeAsync(ChargeRequest request) => null!;
                }
            }
        }

        namespace Contoso
        {
            public class ClientBase { }

            public class PaymentsClient : ClientBase
            {
                public global::Acme.Contracts.PaymentReply ChargeAsync(global::Acme.Contracts.ChargeRequest request) => null!;
            }
        }
        """;
}
