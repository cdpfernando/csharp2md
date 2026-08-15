using Csharp2Md.Core.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Detection;

/// <summary>
/// Detects pub/sub publish and subscribe calls, emitting **half-edges** carrying the topic and the
/// role (P2-03 as amended).
/// </summary>
/// <remarks>
/// A pub/sub edge is only knowable after a publish in one service is matched with a subscribe in
/// another, which happens in Stage 3, long after this document's Markdown is written. So this
/// detector never produces a finished edge: it records what this document proves on its own, and
/// <c>GraphBuilder</c> correlates (P2-14) or retains the signal unpaired (P2-15).
/// </remarks>
public sealed class MessagingDetector : IDocumentDependencyDetector
{
    private static readonly string[] PublishMethods = ["Publish", "PublishAsync"];
    private static readonly string[] SubscribeMethods = ["Subscribe", "SubscribeAsync"];

    public IEnumerable<DependencySignal> Detect(DocumentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var signals = new List<DependencySignal>();

        foreach (var invocation in context.SyntaxTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access)
            {
                continue;
            }

            var method = access.Name.Identifier.ValueText;

            MessagingRole? role = PublishMethods.Contains(method, StringComparer.Ordinal) ? MessagingRole.Publish
                : SubscribeMethods.Contains(method, StringComparer.Ordinal) ? MessagingRole.Subscribe
                : null;

            if (role is not { } messagingRole || Topic(context, invocation, access) is not { } topic)
            {
                continue;
            }

            signals.Add(new DependencySignal(
                SourceService: context.SourceService,
                TargetService: null,
                RawTarget: topic,
                Kind: DependencyKind.Messaging,
                Communication: CommunicationClassifier.Classify(DependencyKind.Messaging, CallShape.NotApplicable),
                // Nothing is resolved yet by construction: the counterpart lives in another service
                // and is not correlated until the graph is assembled. P2-15's unpaired signals keep
                // exactly this value.
                Resolution: ResolutionKind.Unresolved,
                Role: messagingRole,
                Location: new SourceLocation(
                    context.DocumentPath,
                    context.SyntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1)));
        }

        return signals;
    }

    /// <summary>
    /// The message type name, from the most reliable source available: an explicit type argument
    /// (<c>Subscribe&lt;OrderPlaced&gt;</c>), then an explicit construction
    /// (<c>PublishAsync(new OrderPlaced(...))</c>), then the semantic type of the first argument.
    /// The first two need no semantic model, which is why the common shapes keep working degraded.
    /// </summary>
    private static string? Topic(
        DocumentDetectionContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access)
    {
        if (access.Name is GenericNameSyntax { TypeArgumentList.Arguments: [var typeArgument, ..] })
        {
            return SimpleName(typeArgument.ToString());
        }

        if (invocation.ArgumentList.Arguments is not [{ Expression: var first }, ..])
        {
            return null;
        }

        if (first is ObjectCreationExpressionSyntax creation)
        {
            return SimpleName(creation.Type.ToString());
        }

        if (context.SemanticModel is { } model
            && model.SyntaxTree == context.SyntaxTree
            && model.GetTypeInfo(first).Type is { TypeKind: not TypeKind.Error } type)
        {
            return type.Name;
        }

        return null;
    }

    private static string SimpleName(string typeName)
    {
        var lastSeparator = typeName.LastIndexOf('.');
        return lastSeparator >= 0 ? typeName[(lastSeparator + 1)..] : typeName;
    }
}
