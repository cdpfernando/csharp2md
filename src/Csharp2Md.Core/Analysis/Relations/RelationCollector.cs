using System.Text.RegularExpressions;
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
/// Turns <see cref="SyntacticRelationCandidate"/>s into real <see cref="RelationFact"/>s. Every fact
/// this collector produces carries evidence, detector provenance, a null <c>TargetId</c>, and a
/// populated <c>UnresolvedReason</c> (RELC-09/RELC-10/RELC-11) - resolving <c>target_text</c> into a
/// proven target identity is a future <c>RelationResolver</c>'s job, not this collector's.
/// </summary>
internal static class RelationCollector
{
    private const string UnresolvedReasonText =
        "RelationCollector records only the observed syntactic shape; target resolution is deferred to a future RelationResolver.";

    private static readonly DetectorId CollectorDetectorId = DetectorId.Create("io.csharp2md.relation-collector");
    private static readonly FactProvenance Provenance = new("csharp2md.syntax", "1", CollectorDetectorId, "1.0.0");

    private static readonly ImmutableHashSet<string> PublishMemberNames =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Publish", "PublishAsync");

    /// <summary>
    /// Baseline, syntax-only materialization - safe to call unconditionally, with no
    /// <see cref="SemanticModel"/> dependency.
    /// </summary>
    public static ImmutableArray<RelationFact> CreateFacts(
        DocumentFactId documentId,
        string relativePath,
        ImmutableArray<SyntacticRelationCandidate> candidates)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var facts = ImmutableArray.CreateBuilder<RelationFact>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var details = DetailsFor(candidate.RelationKind, candidate.ObservedTarget);
            var ordinal = NextOrdinal(ordinals, candidate.RelationKind, ClaimFor(details));
            var id = RelationFactId.Create(candidate.OwnerId, candidate.RelationKind, ClaimFor(details), ordinal);
            facts.Add(BuildFact(
                id, candidate.OwnerId, documentId, relativePath,
                candidate.StartLine, candidate.StartColumn, candidate.EndLine, candidate.EndColumn,
                candidate.RelationKind, candidate.ShapeConfidence, details));
        }

        return facts.ToImmutable();
    }

    /// <summary>
    /// Semantic-refinement path, called only when a <see cref="SemanticModel"/> bound successfully.
    /// Walks the exact same canonically-ordered <paramref name="candidates"/> list <see cref="CreateFacts"/>
    /// would receive (design.md's named reproducibility risk), so a candidate that refines
    /// successfully mints the identical <see cref="RelationFactId"/> as its baseline counterpart and
    /// <c>FactMerger</c> picks the winner by resolution rank. A candidate that can't be improved (an
    /// error/candidate symbol, or a kind this collector does not refine) simply produces no
    /// enrichment - it never throws and never regresses the baseline. Additionally discovers
    /// <c>publishes</c> relations the syntax-only pass could not read any target from at all (a
    /// <c>PublishAsync(message)</c> call through a variable, with no explicit type argument and no
    /// object-creation argument - spec.md's Assumptions table), resolved here via the invoked method's
    /// constructed generic type argument, exactly as the retired <c>MessagingRelationDetector</c> did.
    /// </summary>
    public static ImmutableArray<RelationFact> Refine(
        DocumentFactId documentId,
        string relativePath,
        ImmutableArray<SyntacticRelationCandidate> candidates,
        SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var facts = ImmutableArray.CreateBuilder<RelationFact>();
        var root = model.SyntaxTree.GetRoot();
        var text = model.SyntaxTree.GetText();

        foreach (var candidate in candidates)
        {
            // The ordinal counter advances for every candidate exactly as CreateFacts' does, whether
            // or not this candidate ends up refined - keeping a later candidate sharing this kind+claim
            // synced to the same ordinal CreateFacts would assign it.
            var details = DetailsFor(candidate.RelationKind, candidate.ObservedTarget);
            var ordinal = NextOrdinal(ordinals, candidate.RelationKind, ClaimFor(details));

            if (candidate.RelationKind is not ("inherits" or "implements"))
            {
                continue;
            }

            var refinedKind = TryResolveBaseListKind(candidate, root, text, model);
            if (refinedKind is null)
            {
                continue;
            }

            var id = RelationFactId.Create(candidate.OwnerId, refinedKind, ClaimFor(details), ordinal);
            facts.Add(BuildFact(
                id, candidate.OwnerId, documentId, relativePath,
                candidate.StartLine, candidate.StartColumn, candidate.EndLine, candidate.EndColumn,
                refinedKind, FactResolution.Syntactic, details));
        }

        facts.AddRange(DiscoverInferredPublishes(documentId, relativePath, root, model, ordinals));

        return facts.ToImmutable();
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
    private static IEnumerable<RelationFact> DiscoverInferredPublishes(
        DocumentFactId documentId,
        string relativePath,
        SyntaxNode root,
        SemanticModel model,
        Dictionary<string, int> ordinals)
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

            // Refine has no access to SyntaxFactExtractor's per-declaration owner map; it falls back to
            // the document itself, the same identity SyntaxFactExtractor.ResolveOwner already documents
            // for code with no local enclosing-member owner (top-level statements).
            var ownerId = documentId.ToFactId();
            var details = DetailsFor("publishes", typeArgument.Name);
            var ordinal = NextOrdinal(ordinals, "publishes", ClaimFor(details));
            var id = RelationFactId.Create(ownerId, "publishes", ClaimFor(details), ordinal);
            var span = model.SyntaxTree.GetLineSpan(invocation.Span);
            yield return BuildFact(
                id, ownerId, documentId, relativePath,
                span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
                span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1,
                "publishes", FactResolution.Syntactic, details);
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

    private static RelationFact BuildFact(
        RelationFactId id,
        FactId sourceId,
        DocumentFactId documentId,
        string relativePath,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        string relationKind,
        FactResolution resolution,
        ImmutableArray<RelationDetail> details)
    {
        var header = FactHeader.Create(
            id.ToFactId(),
            FactKind.Relation,
            resolution,
            [Provenance],
            [new Evidence(documentId, relativePath, startLine, startColumn, endLine, endColumn)]);
        return new RelationFact(header, id, sourceId, null, PartitionFor(relationKind), relationKind, UnresolvedReasonText, details);
    }

    private static int NextOrdinal(Dictionary<string, int> ordinals, string relationKind, string claim)
    {
        var key = $"{relationKind}\0{claim}";
        var ordinal = ordinals.GetValueOrDefault(key) + 1;
        ordinals[key] = ordinal;
        return ordinal;
    }

    private static string ClaimFor(ImmutableArray<RelationDetail> details) =>
        Canonicalize(string.Join('|', details.Select(static detail => $"{detail.Key}={detail.Value}")));

    /// <summary>
    /// Collapses whitespace so the result satisfies <c>FactIdGrammar.RequireCanonicalText</c>. A
    /// detail's raw source text (e.g. <c>http-call</c>'s <c>route</c>, captured verbatim from the
    /// argument expression) can span multiple lines when the expression itself does, e.g. an object
    /// initializer - normalize before it becomes part of a claim fingerprint.
    /// </summary>
    private static string Canonicalize(string value)
    {
        var collapsed = WhitespaceRun.Replace(value, " ");
        return collapsed.Trim();
    }

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

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
