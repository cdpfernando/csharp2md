using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Csharp2Md.Core.Analysis.Relations;

/// <summary>
/// Turns <see cref="SyntacticRelationCandidate"/>s into <see cref="RawRelation"/> claims. Resolving
/// <c>target_text</c> into a proven target identity - and minting the <see cref="RelationFact"/> that
/// carries the outcome - is <c>RelationResolver</c>'s job in pass two (AD-018); this collector only
/// records what pass one observed.
/// </summary>
internal static class RelationCollector
{
    private static readonly ImmutableHashSet<string> PublishMemberNames =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Publish", "PublishAsync");

    /// <summary>
    /// Baseline, syntax-only materialization - safe to call unconditionally, with no
    /// <see cref="SemanticModel"/> dependency.
    /// </summary>
    public static ImmutableArray<RawRelation> CreateClaims(
        DocumentFactId documentId,
        string relativePath,
        ImmutableArray<SyntacticRelationCandidate> candidates)
    {
        var claims = ImmutableArray.CreateBuilder<RawRelation>(candidates.Length);
        foreach (var candidate in candidates)
        {
            claims.Add(BuildClaim(documentId, relativePath, candidate, candidate.RelationKind, candidate.ShapeConfidence));
        }

        return claims.ToImmutable();
    }

    /// <summary>
    /// The claim <see cref="CreateClaims"/> would produce for one candidate, under a possibly-refined
    /// <paramref name="relationKind"/> and <paramref name="shapeConfidence"/> - shared with
    /// <see cref="RefineClaims"/> so a refined candidate's claim carries exactly the same fields a
    /// baseline claim would, save for the refinement itself.
    /// </summary>
    private static RawRelation BuildClaim(
        DocumentFactId documentId,
        string relativePath,
        SyntacticRelationCandidate candidate,
        string relationKind,
        FactResolution shapeConfidence) =>
        new()
        {
            Kind = relationKind,
            OwnerId = candidate.OwnerId,
            Evidence = new Evidence(
                documentId, relativePath,
                candidate.StartLine, candidate.StartColumn, candidate.EndLine, candidate.EndColumn),
            ShapeConfidence = shapeConfidence,
            Partition = PartitionFor(relationKind),
            Details = DetailsFor(relationKind, candidate.ObservedTarget),
            TargetText = candidate.ObservedTarget,
            ReceiverText = candidate.ReceiverText,
            ReceiverTypeText = candidate.ReceiverTypeText,
            MemberName = candidate.MemberName,
            ArgumentCount = candidate.ArgumentCount,
            ArgumentTypes = candidate.ArgumentTypes,
            Namespace = candidate.Namespace,
            Imports = candidate.Imports,
        };

    /// <summary>
    /// Semantic-refinement path, called only when a <see cref="SemanticModel"/> bound successfully.
    /// Starts from the exact same <see cref="CreateClaims"/> baseline the syntax-only pass would
    /// produce for <paramref name="candidates"/>, then merges a refined base-list claim into its
    /// baseline counterpart by <see cref="FactResolutionAlgebra.Stronger"/> (AD-018) rather than
    /// relying on a later fact-level rank merge. A candidate that can't be improved (an error/candidate
    /// symbol, or a kind this collector does not refine) leaves its baseline claim untouched in the
    /// result - it is never dropped. Additionally discovers <c>publishes</c> relations the syntax-only
    /// pass could not read any target from at all (a <c>PublishAsync(message)</c> call through a
    /// variable, with no explicit type argument and no object-creation argument - spec.md's Assumptions
    /// table), resolved here via the invoked method's constructed generic type argument, exactly as the
    /// retired <c>MessagingRelationDetector</c> did.
    /// </summary>
    public static ImmutableArray<RawRelation> RefineClaims(
        DocumentFactId documentId,
        string relativePath,
        ImmutableArray<SyntacticRelationCandidate> candidates,
        SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var claims = CreateClaims(documentId, relativePath, candidates).ToArray();
        var root = model.SyntaxTree.GetRoot();
        var text = model.SyntaxTree.GetText();

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (candidate.RelationKind is not ("inherits" or "implements"))
            {
                continue;
            }

            var refinedKind = TryResolveBaseListKind(candidate, root, text, model);
            if (refinedKind is null)
            {
                continue;
            }

            var baseline = claims[index];
            claims[index] = baseline with
            {
                Kind = refinedKind,
                Partition = PartitionFor(refinedKind),
                ShapeConfidence = FactResolutionAlgebra.Stronger(baseline.ShapeConfidence, FactResolution.Syntactic),
            };
        }

        var merged = ImmutableArray.CreateBuilder<RawRelation>(claims.Length);
        merged.AddRange(claims);
        merged.AddRange(DiscoverInferredPublishClaims(documentId, relativePath, root, model));

        return merged.ToImmutable();
    }

    /// <summary>
    /// Resolves a base-list entry's real <c>TypeKind</c> (class vs. interface) via the semantic model,
    /// upgrading the syntax-only naming-convention guess to a certain classification. Returns
    /// <c>null</c> - never throws - when the node can't be located or resolves to an error/candidate
    /// symbol (RELC-08).
    /// </summary>
    private static string? TryResolveBaseListKind(
        SyntacticRelationCandidate candidate, SyntaxNode root, SourceText text, SemanticModel model)
    {
        try
        {
            var span = SpanFor(candidate.StartLine, candidate.StartColumn, candidate.EndLine, candidate.EndColumn, text);
            var node = root.FindNode(span, getInnermostNodeForTie: true);
            var symbol = model.GetSymbolInfo(node).Symbol as ITypeSymbol ?? model.GetTypeInfo(node).Type;
            if (symbol is null || symbol.TypeKind is TypeKind.Error)
            {
                return null;
            }

            return symbol.TypeKind is TypeKind.Interface ? "implements" : "inherits";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// A <c>Publish</c>/<c>PublishAsync</c> invocation with neither an explicit generic type argument
    /// nor an object-creation-expression argument - the one shape <see cref="SyntaxFactExtractor"/>
    /// deliberately emits no candidate for (spec.md's Assumptions table) - resolved here via the real
    /// invoked method's constructed type argument when the receiver implements a qualifying
    /// publish-shaped interface member (ported from the retired <c>MessagingRelationDetector</c>).
    /// </summary>
    private static IEnumerable<RawRelation> DiscoverInferredPublishClaims(
        DocumentFactId documentId,
        string relativePath,
        SyntaxNode root,
        SemanticModel model)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsUninferredPublishInvocation(invocation))
            {
                continue;
            }

            if (model.GetOperation(invocation) is not IInvocationOperation operation)
            {
                continue;
            }

            var method = operation.TargetMethod;
            if (method.TypeArguments.Length != 1 || !QualifyingCandidates(operation).Any(IsPublishShape))
            {
                continue;
            }

            var typeArgument = method.TypeArguments[0];
            if (typeArgument.TypeKind is TypeKind.Error)
            {
                continue;
            }

            // RefineClaims has no access to SyntaxFactExtractor's per-declaration owner map; it falls
            // back to the document itself, the same identity SyntaxFactExtractor.ResolveOwner already
            // documents for code with no local enclosing-member owner (top-level statements).
            var ownerId = documentId.ToFactId();
            var details = DetailsFor("publishes", typeArgument.Name);
            var span = model.SyntaxTree.GetLineSpan(invocation.Span);
            yield return new RawRelation
            {
                Kind = "publishes",
                OwnerId = ownerId,
                Evidence = new Evidence(
                    documentId, relativePath,
                    span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
                    span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1),
                ShapeConfidence = FactResolution.Syntactic,
                Partition = PartitionFor("publishes"),
                Details = details,
                TargetText = typeArgument.Name,
            };
        }
    }

    private static bool IsUninferredPublishInvocation(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax memberAccess
        && PublishMemberNames.Contains(memberAccess.Name.Identifier.ValueText)
        && memberAccess.Name is not GenericNameSyntax
        && invocation.ArgumentList.Arguments is not [{ Expression: ObjectCreationExpressionSyntax }, ..];

    /// <summary>The invoked symbol itself plus every same-named method on an interface the receiver implements.</summary>
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
        var definition = method.ConstructedFrom;
        return definition.Name is "Publish" or "PublishAsync"
            && definition.ContainingType.TypeKind == TypeKind.Interface
            && definition.TypeParameters.Length == 1
            && definition.Parameters.Length > 0
            && SymbolEqualityComparer.Default.Equals(definition.Parameters[0].Type, definition.TypeParameters[0]);
    }

    private static TextSpan SpanFor(int startLine, int startColumn, int endLine, int endColumn, SourceText text)
    {
        var start = text.Lines[startLine - 1].Start + (startColumn - 1);
        var end = text.Lines[endLine - 1].Start + (endColumn - 1);
        return TextSpan.FromBounds(start, end);
    }

    /// <summary>
    /// Every kind wraps its observed target as one <c>target_text</c> detail, except
    /// <c>http-call</c>, whose <see cref="SyntacticRelationCandidate.ObservedTarget"/> is encoded as
    /// pipe-delimited <c>key=value</c> pairs (<c>http_method=GET|route=/orders</c>) and splits into
    /// its own two named details instead of one opaque string.
    /// </summary>
    private static ImmutableArray<RelationDetail> DetailsFor(string relationKind, string observedTarget)
    {
        if (relationKind != "http-call")
        {
            return [new RelationDetail("target_text", observedTarget)];
        }

        var details = ImmutableArray.CreateBuilder<RelationDetail>();
        foreach (var part in observedTarget.Split('|'))
        {
            var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            details.Add(separatorIndex < 0
                ? new RelationDetail("target_text", part)
                : new RelationDetail(part[..separatorIndex], part[(separatorIndex + 1)..]));
        }

        return details.Distinct().Order().ToImmutableArray();
    }

    private static RelationPartition PartitionFor(string relationKind) => relationKind switch
    {
        "inherits" or "implements" => RelationPartition.Inheritance,
        "publishes" or "subscribes" or "handles" => RelationPartition.Events,
        "http-client" or "http-call" => RelationPartition.Http,
        "calls" or "creates" or "references" => RelationPartition.Structural,
        _ => throw new ArgumentOutOfRangeException(nameof(relationKind), relationKind, "Unsupported relation kind."),
    };
}
