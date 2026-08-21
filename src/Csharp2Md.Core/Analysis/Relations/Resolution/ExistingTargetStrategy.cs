using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Relations.Resolution;

/// <summary>
/// Design's strategy #1: a claim a producing stage already targeted (e.g. <c>DatabaseMappingResolver</c>'s
/// configured mappings) needs no lookup - only a check that both ends of the edge still exist in the run
/// (RELR-02, RELR-03). Declines a claim with no target at all (RELR story-1 AC2 requires "already carries a
/// non-null target_id"), leaving it to a strategy that can actually look one up.
/// </summary>
internal sealed class ExistingTargetStrategy : IRelationResolutionStrategy
{
    private const string MissingFactCode = "C2M-RELR-007";

    public RelationResolutionOutcome TryResolve(RelationResolutionContext context)
    {
        var claim = context.Relation;
        if (claim.TargetId is not { } targetId)
        {
            return RelationResolutionOutcome.None;
        }

        var sourceMissing = !context.KnownFactIds.Contains(claim.OwnerId);
        var targetMissing = !context.KnownFactIds.Contains(targetId);
        if (!sourceMissing && !targetMissing)
        {
            return new RelationResolutionOutcome(
                Handled: true,
                TargetId: targetId,
                Method: claim.ProducerMethod ?? ResolutionMethod.Exact);
        }

        var data = ImmutableArray.CreateBuilder<DiagnosticData>();
        data.Add(new DiagnosticData("relation_kind", claim.Kind));
        if (sourceMissing)
        {
            data.Add(new DiagnosticData("missing_source_id", claim.OwnerId.Value));
        }

        if (targetMissing)
        {
            data.Add(new DiagnosticData("missing_target_id", targetId.Value));
        }

        return new RelationResolutionOutcome(
            Handled: true,
            TargetId: targetMissing ? null : targetId,
            Method: targetMissing ? ResolutionMethod.Unresolved : (claim.ProducerMethod ?? ResolutionMethod.Exact),
            UnresolvedReason: targetMissing
                ? $"Target '{targetId.Value}' is absent from the run's fact set."
                : null,
            Diagnostic: new RelationDiagnostic(
                MissingFactCode,
                DiagnosticSeverity.Warning,
                "The relation names a source or target fact absent from the run.",
                data.ToImmutable()));
    }
}
