using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using FactDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;

namespace Csharp2Md.Core.Detection.Grpc;

internal sealed class GrpcRelationDetector : IDocumentFactDetector
{
    private const string GrpcNamespace = "Grpc.Core";
    private const string ClientBaseName = "ClientBase";
    private const string ClientSuffix = "Client";
    private const string CallKind = "grpc-call";

    private static readonly string[] StreamingCallTypes =
        ["AsyncServerStreamingCall", "AsyncClientStreamingCall", "AsyncDuplexStreamingCall"];

    // ClientBase members that configure the client instead of issuing an RPC.
    private static readonly string[] NonRpcMembers = ["WithHost"];

    public DetectorDescriptor Descriptor { get; } = DetectorDescriptor.Create(
        DetectorId.Create("io.csharp2md.grpc"),
        "1.0.0",
        [DetectorLevel.Document],
        [FactKind.Relation]);

    public DetectorResult Detect(FactDocumentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.SemanticDocument is not { } semantic)
        {
            return DetectorResult.Create();
        }

        var root = semantic.SyntaxTree.GetRoot();
        var observations = ImmutableArray.CreateBuilder<Observation>();
        foreach (var syntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semantic.SemanticModel.GetOperation(syntax) is not IInvocationOperation invocation)
            {
                continue;
            }

            Observe(context, semantic, invocation, observations);
        }

        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var facts = observations
            .OrderBy(static observation => observation.Evidence)
            .Select(observation => CreateFact(context, observation, ordinals))
            .ToImmutableArray();
        return DetectorResult.Create(facts);
    }

    private static void Observe(
        FactDocumentDetectionContext context,
        SemanticDetectionDocument semantic,
        IInvocationOperation invocation,
        ImmutableArray<Observation>.Builder observations)
    {
        var method = invocation.TargetMethod;
        if (NonRpcMembers.Contains(method.Name, StringComparer.Ordinal)
            || invocation.Instance?.Type is not { } receiverType
            || !IsGeneratedGrpcClient(receiverType))
        {
            return;
        }

        var details = new List<RelationDetail>
        {
            new("service", ServiceNameFrom(receiverType)),
            new("method", method.Name),
            new("call_shape", CallShapeOf(method)),
        };

        observations.Add(new Observation(
            FactResolution.Exact,
            EvidenceFor(context.Document, semantic.SyntaxTree, invocation.Syntax),
            details.ToImmutableArray()));
    }

    private static bool IsGeneratedGrpcClient(ITypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Name == ClientBaseName && current.ContainingNamespace?.ToDisplayString() == GrpcNamespace)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A unary exchange blocks on one response; a streaming or duplex exchange does not. A
    /// generated blocking overload returns the response type directly rather than a call handle,
    /// which is still a unary request/response.
    /// </summary>
    private static string CallShapeOf(IMethodSymbol method) =>
        method.ReturnType.ContainingNamespace?.ToDisplayString() == GrpcNamespace
        && StreamingCallTypes.Contains(method.ReturnType.Name, StringComparer.Ordinal)
            ? "streaming"
            : "unary";

    /// <summary>The logical target of a generated client is the proto service it was generated for.</summary>
    private static string ServiceNameFrom(ITypeSymbol client) =>
        client.Name.Length > ClientSuffix.Length && client.Name.EndsWith(ClientSuffix, StringComparison.Ordinal)
            ? client.Name[..^ClientSuffix.Length]
            : client.Name;

    private static RelationFact CreateFact(
        FactDocumentDetectionContext context,
        Observation observation,
        Dictionary<string, int> ordinals)
    {
        var details = observation.Details.Distinct().Order().ToImmutableArray();
        var claim = string.Join('|', details.Select(static detail => $"{detail.Key}={detail.Value}"));
        var ordinalKey = $"{CallKind}\0{claim}";
        var ordinal = ordinals.GetValueOrDefault(ordinalKey) + 1;
        ordinals[ordinalKey] = ordinal;
        var id = RelationFactId.Create(context.Document.DocumentId.ToFactId(), CallKind, claim, ordinal);
        var header = FactHeader.Create(
            id.ToFactId(),
            FactKind.Relation,
            observation.Resolution,
            evidence: [observation.Evidence]);
        return new RelationFact(
            header,
            id,
            context.Document.DocumentId.ToFactId(),
            null,
            RelationPartition.Grpc,
            CallKind,
            "gRPC source observation does not identify a remote service target.",
            details);
    }

    private static Evidence EvidenceFor(DocumentFact document, SyntaxTree syntaxTree, SyntaxNode node)
    {
        var span = syntaxTree.GetLineSpan(node.Span);
        return new Evidence(
            document.DocumentId,
            document.RelativePath,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    private sealed record Observation(FactResolution Resolution, Evidence Evidence, ImmutableArray<RelationDetail> Details);
}
