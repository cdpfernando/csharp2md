using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

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

    /// <summary>
    /// Baseline, syntax-only materialization - safe to call unconditionally, with no
    /// <see cref="Microsoft.CodeAnalysis.SemanticModel"/> dependency.
    /// </summary>
    public static ImmutableArray<RelationFact> CreateFacts(
        DocumentFactId documentId,
        string relativePath,
        ImmutableArray<SyntacticRelationCandidate> candidates)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        return candidates
            .Select(candidate => CreateFact(documentId, relativePath, candidate, ordinals))
            .ToImmutableArray();
    }

    private static RelationFact CreateFact(
        DocumentFactId documentId,
        string relativePath,
        SyntacticRelationCandidate candidate,
        Dictionary<string, int> ordinals)
    {
        var details = DetailsFor(candidate.RelationKind, candidate.ObservedTarget);
        var claim = string.Join('|', details.Select(static detail => $"{detail.Key}={detail.Value}"));
        var ordinalKey = $"{candidate.RelationKind}\0{claim}";
        var ordinal = ordinals.GetValueOrDefault(ordinalKey) + 1;
        ordinals[ordinalKey] = ordinal;
        var id = RelationFactId.Create(candidate.OwnerId, candidate.RelationKind, claim, ordinal);
        var header = FactHeader.Create(
            id.ToFactId(),
            FactKind.Relation,
            candidate.ShapeConfidence,
            [Provenance],
            [EvidenceFor(documentId, relativePath, candidate)]);
        return new RelationFact(
            header,
            id,
            candidate.OwnerId,
            null,
            PartitionFor(candidate.RelationKind),
            candidate.RelationKind,
            UnresolvedReasonText,
            details);
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

    private static Evidence EvidenceFor(DocumentFactId documentId, string relativePath, SyntacticRelationCandidate candidate) =>
        new(
            documentId,
            relativePath,
            candidate.StartLine,
            candidate.StartColumn,
            candidate.EndLine,
            candidate.EndColumn);
}
