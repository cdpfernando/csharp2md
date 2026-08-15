using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Detection;

/// <summary>
/// Detects calls on a generated gRPC client and separates unary from streaming exchanges (P2-02).
/// </summary>
/// <remarks>
/// Recognition is semantic: the receiver's type must actually derive from <c>Grpc.Core.ClientBase</c>,
/// and the shape comes from the call's return type (<c>AsyncUnaryCall</c> versus one of the
/// streaming call types). Without a semantic model this detector reports nothing rather than
/// guessing from identifier names, matching how the typed-<c>HttpClient</c> path degrades.
/// <para>
/// SPEC_DEVIATION: emitted signals carry <c>TargetService = null</c>, for the same reason recorded
/// on <see cref="HttpClientDetector"/> - no rule mapping a logical name to a catalog service exists
/// in spec.md or design.md, so correlation is left to the graph builder rather than guessed here.
/// </para>
/// </remarks>
public sealed class GrpcClientDetector : IDocumentDependencyDetector
{
    private const string GrpcNamespace = "Grpc.Core";
    private const string ClientBaseName = "ClientBase";
    private const string ClientSuffix = "Client";

    private static readonly string[] StreamingCallTypes =
        ["AsyncServerStreamingCall", "AsyncClientStreamingCall", "AsyncDuplexStreamingCall"];

    // ClientBase members that configure the client instead of issuing an RPC.
    private static readonly string[] NonRpcMembers = ["WithHost"];

    public IEnumerable<DependencySignal> Detect(DocumentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SemanticModel is not { } model || model.SyntaxTree != context.SyntaxTree)
        {
            return [];
        }

        var signals = new List<DependencySignal>();

        foreach (var invocation in context.SyntaxTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access
                || NonRpcMembers.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal)
                || model.GetTypeInfo(access.Expression).Type is not { } receiver
                || !IsGeneratedGrpcClient(receiver))
            {
                continue;
            }

            var rawTarget = ServiceNameFrom(receiver);
            var resolution = ServiceNameResolver.Resolve(rawTarget, context.ConfigIndex);

            signals.Add(new DependencySignal(
                SourceService: context.SourceService,
                TargetService: null,
                RawTarget: rawTarget,
                Kind: DependencyKind.Grpc,
                Communication: CommunicationClassifier.Classify(DependencyKind.Grpc, ShapeOf(model, invocation)),
                Resolution: resolution.Kind,
                Role: null,
                Location: new SourceLocation(
                    context.DocumentPath,
                    context.SyntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1)));
        }

        return signals;
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
    /// P2-02: a unary exchange blocks on one response; a streaming or duplex exchange does not.
    /// A generated blocking overload returns the response type directly rather than a call handle,
    /// which is still a unary request/response.
    /// </summary>
    private static CallShape ShapeOf(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (model.GetTypeInfo(invocation).Type is not { } returned
            || returned.ContainingNamespace?.ToDisplayString() != GrpcNamespace)
        {
            return CallShape.Unary;
        }

        return StreamingCallTypes.Contains(returned.Name, StringComparer.Ordinal)
            ? CallShape.Streaming
            : CallShape.Unary;
    }

    /// <summary>
    /// The logical target of a generated client is the proto service it was generated for:
    /// <c>PaymentsClient</c> is the client for <c>Payments</c>.
    /// </summary>
    private static string ServiceNameFrom(ITypeSymbol client) =>
        client.Name.Length > ClientSuffix.Length && client.Name.EndsWith(ClientSuffix, StringComparison.Ordinal)
            ? client.Name[..^ClientSuffix.Length]
            : client.Name;
}
