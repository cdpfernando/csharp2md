using Csharp2Md.Core.Detection.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using FactDocumentDetectionContext = Csharp2Md.Core.Detection.Contracts.DocumentDetectionContext;

namespace Csharp2Md.Core.Detection.Messaging;

/// <summary>
/// Confirms producer/consumer calls by shape, not by name alone (FACT-46): a qualifying
/// interface method named <c>Publish</c>/<c>PublishAsync</c> whose single generic type parameter
/// is used directly as the message parameter, or <c>Subscribe</c>/<c>SubscribeAsync</c> whose
/// single generic type parameter is threaded into a handler-shaped parameter. No specific
/// messaging package is referenced by spec.md or design.md, so the contract is recognized by this
/// structural shape rather than one hardcoded namespace, mirroring the fixture's own IEventBus.
/// </summary>
internal sealed class MessagingRelationDetector : IDocumentFactDetector
{
    private const string PublishKind = "messaging-publish";
    private const string SubscribeKind = "messaging-subscribe";

    public DetectorDescriptor Descriptor { get; } = DetectorDescriptor.Create(
        DetectorId.Create("io.csharp2md.messaging"),
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
            .ThenBy(static observation => observation.Kind, StringComparer.Ordinal)
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
        if (method.TypeArguments.Length != 1)
        {
            return;
        }

        var candidates = QualifyingCandidates(invocation);
        string kind;
        if (candidates.Any(IsPublishShape))
        {
            kind = PublishKind;
        }
        else if (candidates.Any(IsSubscribeShape))
        {
            kind = SubscribeKind;
        }
        else
        {
            return;
        }

        observations.Add(new Observation(
            kind,
            EvidenceFor(context.Document, semantic.SyntaxTree, invocation.Syntax),
            [new RelationDetail("topic", method.TypeArguments[0].Name)]));
    }

    /// <summary>
    /// The invoked symbol itself (covers an interface-typed call site) plus every same-named
    /// method on an interface the receiver implements (covers a concrete-typed call site).
    /// </summary>
    private static IEnumerable<IMethodSymbol> QualifyingCandidates(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        yield return method;

        if (invocation.Instance?.Type is not { } receiverType)
        {
            yield break;
        }

        foreach (var candidateInterface in receiverType.AllInterfaces)
        {
            foreach (var candidate in candidateInterface.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                yield return candidate;
            }
        }
    }

    private static bool IsPublishShape(IMethodSymbol method)
    {
        // ConstructedFrom undoes call-site type substitution: a constructed PublishAsync<OrderPlaced>
        // has Parameters[0].Type == OrderPlaced, which can never equal the unsubstituted
        // TypeParameters[0] (TEvent) - only the generic definition keeps both consistent.
        var definition = method.ConstructedFrom;
        return definition.Name is "Publish" or "PublishAsync"
            && definition.ContainingType.TypeKind == TypeKind.Interface
            && definition.TypeParameters.Length == 1
            && definition.Parameters.Length > 0
            && SymbolEqualityComparer.Default.Equals(definition.Parameters[0].Type, definition.TypeParameters[0]);
    }

    private static bool IsSubscribeShape(IMethodSymbol method)
    {
        var definition = method.ConstructedFrom;
        return definition.Name is "Subscribe" or "SubscribeAsync"
            && definition.ContainingType.TypeKind == TypeKind.Interface
            && definition.TypeParameters.Length == 1
            && definition.Parameters.Any(parameter => ReferencesTypeParameter(parameter.Type, definition.TypeParameters[0]));
    }

    /// <summary>Does a handler parameter's type (e.g. <c>Func&lt;TEvent, CancellationToken, Task&gt;</c>) mention the message type parameter?</summary>
    private static bool ReferencesTypeParameter(ITypeSymbol type, ITypeParameterSymbol typeParameter) =>
        type is INamedTypeSymbol named
        && named.TypeArguments.Any(argument => SymbolEqualityComparer.Default.Equals(argument, typeParameter));

    private static RelationFact CreateFact(
        FactDocumentDetectionContext context,
        Observation observation,
        Dictionary<string, int> ordinals)
    {
        var details = observation.Details.Distinct().Order().ToImmutableArray();
        var claim = string.Join('|', details.Select(static detail => $"{detail.Key}={detail.Value}"));
        var ordinalKey = $"{observation.Kind}\0{claim}";
        var ordinal = ordinals.GetValueOrDefault(ordinalKey) + 1;
        ordinals[ordinalKey] = ordinal;
        var id = RelationFactId.Create(context.Document.DocumentId.ToFactId(), observation.Kind, claim, ordinal);
        var header = FactHeader.Create(
            id.ToFactId(),
            FactKind.Relation,
            FactResolution.Exact,
            evidence: [observation.Evidence]);
        return new RelationFact(
            header,
            id,
            context.Document.DocumentId.ToFactId(),
            null,
            RelationPartition.Events,
            observation.Kind,
            "A single document proves only its own side of a messaging exchange; the counterpart lives elsewhere.",
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

    private sealed record Observation(string Kind, Evidence Evidence, ImmutableArray<RelationDetail> Details);
}
