using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.DataAccess.Sql;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Relations.Resolution;

/// <summary>
/// Design's strategy #2: owns every <c>data</c>-partition claim. A configured target already arrived
/// proven from <c>DatabaseMappingResolver</c> and would normally be intercepted earlier by
/// <see cref="ExistingTargetStrategy"/>; this strategy's own configured branch exists for its unit
/// tests and for the (currently unreachable in production) case of testing it standalone, per the
/// task's own Done-when. Everything left unproven is classified by its <c>mapping</c> detail (RELR-27,
/// RELR-28) or by whether its SQL target came from an interpolated/concatenated expression (RELR-30) -
/// never guessed, and never a database node created, altered or deleted (RELR-31).
/// </summary>
internal sealed class DatabaseRelationStrategy : IRelationResolutionStrategy
{
    private const string MappingDetailKey = "mapping";
    private const string ConventionCode = "C2M-RELR-005";
    private const string DynamicCode = "C2M-RELR-004";
    private const string NoCandidateCode = "C2M-RELR-001";

    public RelationResolutionOutcome TryResolve(RelationResolutionContext context)
    {
        var claim = context.Relation;
        if (claim.Partition is not RelationPartition.Data)
        {
            return RelationResolutionOutcome.None;
        }

        if (claim.TargetId is { } targetId)
        {
            return new RelationResolutionOutcome(Handled: true, TargetId: targetId, Method: ResolutionMethod.Configured);
        }

        if (string.Equals(DetailValue(claim.Details, MappingDetailKey), DatabaseMappingResolver.ConventionMapping, StringComparison.Ordinal))
        {
            return new RelationResolutionOutcome(
                Handled: true,
                Method: ResolutionMethod.Convention,
                UnresolvedReason: claim.UnresolvedReason,
                Diagnostic: new RelationDiagnostic(
                    ConventionCode,
                    DiagnosticSeverity.Information,
                    $"Target reached by naming convention, not by configuration, for a '{claim.Kind}' relation.",
                    [new DiagnosticData("relation_kind", claim.Kind)]));
        }

        if (string.Equals(claim.UnresolvedReason, SqlTextAnalyzer.DynamicSqlReason, StringComparison.Ordinal))
        {
            return new RelationResolutionOutcome(
                Handled: true,
                Method: ResolutionMethod.Dynamic,
                UnresolvedReason: claim.UnresolvedReason,
                Diagnostic: new RelationDiagnostic(
                    DynamicCode,
                    DiagnosticSeverity.Information,
                    $"Target depends on a value computed at runtime for a '{claim.Kind}' relation.",
                    [new DiagnosticData("relation_kind", claim.Kind)]));
        }

        // Every other data-partition claim without a proven target: an unreadable SQL target, an
        // unmapped owning object, or an ambiguous entity attribution. None of these are "configured
        // vs. convention vs. dynamic" per RELR-27..RELR-30, so they fall to the generic unresolved
        // outcome, keeping whichever reason DatabaseMappingResolver already recorded.
        return new RelationResolutionOutcome(
            Handled: true,
            Method: ResolutionMethod.Unresolved,
            UnresolvedReason: string.IsNullOrWhiteSpace(claim.UnresolvedReason)
                ? $"No candidate found for a '{claim.Kind}' relation."
                : claim.UnresolvedReason,
            Diagnostic: new RelationDiagnostic(
                NoCandidateCode,
                DiagnosticSeverity.Information,
                $"No candidate was found for the observed target text of a '{claim.Kind}' relation.",
                [new DiagnosticData("relation_kind", claim.Kind)]));
    }

    private static string? DetailValue(ImmutableArray<RelationDetail> details, string key)
    {
        foreach (var detail in details)
        {
            if (string.Equals(detail.Key, key, StringComparison.Ordinal))
            {
                return detail.Value;
            }
        }

        return null;
    }
}
