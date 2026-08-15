using Csharp2Md.Core.Configuration;
using Csharp2Md.Core.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Detection;

/// <summary>
/// Detects HTTP client dependencies (P2-01): named clients obtained from
/// <c>IHttpClientFactory.CreateClient(...)</c>, and calls made on a typed <c>HttpClient</c>.
/// </summary>
/// <remarks>
/// SPEC_DEVIATION: emitted signals carry <c>TargetService = null</c>.
/// Reason: <c>ServiceNameResolver</c> (T9) classifies *how* a logical name resolves but does not
/// map it to a catalog service, and no rule for that mapping exists in spec.md or design.md - the
/// fixture deliberately uses a config key ("PaymentService") that does not equal any catalog
/// service name ("Acme.Payments"). Inventing a match here (substring, fuzzy, or otherwise) would
/// fabricate graph edges. The raw target and resolution classification are recorded per P2-09;
/// correlating a target to a catalog service is left to the graph builder (T19), which is where
/// the whole catalog is in scope.
/// </remarks>
public sealed class HttpClientDetector : IDocumentDependencyDetector
{
    private const string CreateClientMethod = "CreateClient";
    private const string HttpClientTypeName = "System.Net.Http.HttpClient";

    // Members of HttpClient that are not themselves a request.
    private static readonly string[] NonRequestMembers = ["Dispose", "CancelPendingRequests"];

    public IEnumerable<DependencySignal> Detect(DocumentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var root = context.SyntaxTree.GetRoot();
        var signals = new List<DependencySignal>();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access)
            {
                continue;
            }

            var rawTarget = NamedClientTarget(invocation, access) ?? TypedClientTarget(context, invocation, access);
            if (rawTarget is null)
            {
                continue;
            }

            var resolution = ServiceNameResolver.Resolve(rawTarget, context.ConfigIndex);

            signals.Add(new DependencySignal(
                SourceService: context.SourceService,
                TargetService: null,
                RawTarget: rawTarget,
                Kind: DependencyKind.Http,
                Communication: CommunicationClassifier.Classify(DependencyKind.Http, ShapeOf(invocation)),
                Resolution: resolution.Kind,
                Role: null,
                Location: Location(context, invocation)));
        }

        return signals;
    }

    /// <summary>
    /// <c>CreateClient("Name")</c>. Recognised by name alone so it keeps working without a semantic
    /// model, which is the whole point of a factory-registered logical name.
    /// </summary>
    private static string? NamedClientTarget(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax access)
    {
        if (access.Name.Identifier.ValueText != CreateClientMethod)
        {
            return null;
        }

        return invocation.ArgumentList.Arguments is [{ Expression: LiteralExpressionSyntax literal }, ..]
            && literal.Token.Value is string name
            && !string.IsNullOrWhiteSpace(name)
                ? name
                : null;
    }

    /// <summary>
    /// A call on something whose type really is <c>HttpClient</c>. This one needs the semantic
    /// model; without it the detector reports nothing here rather than guessing from identifier
    /// names, so it degrades with the renderer instead of inventing edges.
    /// </summary>
    private static string? TypedClientTarget(
        DocumentDetectionContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access)
    {
        if (context.SemanticModel is not { } model
            || model.SyntaxTree != context.SyntaxTree
            || NonRequestMembers.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal)
            || IsChainedOffCreateClient(access)
            || !IsHttpClient(model.GetTypeInfo(access.Expression).Type))
        {
            return null;
        }

        // The request URI is the closest thing to a target name a typed client exposes.
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax { Token.Value: string uri } && uri.Length > 0)
            {
                return uri;
            }
        }

        return access.Expression.ToString();
    }

    /// <summary>
    /// <c>factory.CreateClient("X").GetAsync(u)</c> is one dependency, not two: the named-client
    /// path already reported it with the better target name.
    /// </summary>
    private static bool IsChainedOffCreateClient(MemberAccessExpressionSyntax access) =>
        access.Expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: CreateClientMethod },
        };

    private static bool IsHttpClient(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == HttpClientTypeName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// P2-01: awaited or result consumed means the caller blocks; a discarded result is
    /// fire-and-forget. The walk climbs the surrounding call chain so
    /// <c>ConfigureAwait</c>/<c>Result</c>/<c>Wait</c> are judged on the whole expression.
    /// </summary>
    internal static CallShape ShapeOf(InvocationExpressionSyntax invocation)
    {
        SyntaxNode node = invocation;
        var parent = node.Parent;
        var blocks = false;

        while (parent is not null)
        {
            if (parent is MemberAccessExpressionSyntax member && member.Expression == node)
            {
                blocks |= member.Name.Identifier.ValueText is "Result" or "Wait" or "GetResult";
            }
            else if (parent is not (ParenthesizedExpressionSyntax or CastExpressionSyntax)
                && !(parent is InvocationExpressionSyntax outer && outer.Expression == node))
            {
                break;
            }

            node = parent;
            parent = parent.Parent;
        }

        if (blocks || parent is AwaitExpressionSyntax)
        {
            return CallShape.ResultConsumed;
        }

        return parent switch
        {
            ExpressionStatementSyntax => CallShape.ResultDiscarded,
            AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "_" } } =>
                CallShape.ResultDiscarded,
            _ => CallShape.ResultConsumed,
        };
    }

    private static SourceLocation Location(DocumentDetectionContext context, SyntaxNode node) => new(
        context.DocumentPath,
        context.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line + 1);
}
