using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Relations.Resolution;

/// <summary>
/// Resolves everything else with a <c>target_text</c> through a name lookup (design.md's strategy #4):
/// a claim already owned by an earlier strategy in the chain never reaches this one (RELR-09), so this
/// strategy applies one uniform rule regardless of relation kind - whatever is left with an observed
/// name gets looked up by that name through <see cref="ISymbolIndex.FindCandidates"/>, which already
/// decides <c>Unique</c>/<c>Ambiguous</c>/<c>NotFound</c> without choosing a winner (RELR-14 is
/// enforced by the index, not re-implemented here).
/// </summary>
internal sealed class SymbolIndexStrategy : IRelationResolutionStrategy
{
    private const string AmbiguousCode = "C2M-RELR-002";

    public RelationResolutionOutcome TryResolve(RelationResolutionContext context)
    {
        var claim = context.Relation;
        if (string.IsNullOrWhiteSpace(claim.TargetText))
        {
            return RelationResolutionOutcome.None;
        }

        var lookup = new SymbolLookup
        {
            Name = claim.TargetText,
            Namespace = claim.Namespace,
            ProjectId = claim.ProjectId,
            Imports = claim.Imports,
        };

        var result = context.SymbolIndex.FindCandidates(lookup);
        return result.Status switch
        {
            // Nothing found declines to UnresolvedStrategy (the terminal strategy), which already
            // owns "no candidate found" - handling it here too would duplicate that diagnostic.
            SymbolLookupStatus.NotFound => RelationResolutionOutcome.None,
            SymbolLookupStatus.Ambiguous => Ambiguous(claim, result),
            _ => Unique(claim, result),
        };
    }

    /// <summary>
    /// RELR-18: the resolved symbol's own declaration span is appended as additional evidence when it
    /// is not already the claim's own span - a call site and its target's declaration are almost always
    /// different locations, but the filter keeps a coincidental match from duplicating an entry.
    /// </summary>
    private static RelationResolutionOutcome Unique(RawRelation claim, SymbolLookupResult result)
    {
        var target = result.Candidates[0];
        var addedEvidence = target.Header.Evidence
            .Where(evidence => evidence != claim.Evidence)
            .ToImmutableArray();

        return new RelationResolutionOutcome(
            Handled: true,
            TargetId: target.SymbolId.ToFactId(),
            Method: ResolutionMethod.Syntactic,
            AddedEvidence: addedEvidence);
    }

    /// <summary>
    /// RELR story-2 AC2/AC3: never pick from a multi-entry best tier. <see cref="SymbolLookupResult.TiedCandidateCount"/>
    /// is exactly the leading, tied-at-best-rank prefix of <see cref="SymbolLookupResult.Candidates"/> -
    /// already ordinal-ordered by the index, so no re-sort is needed here.
    /// </summary>
    private static RelationResolutionOutcome Ambiguous(RawRelation claim, SymbolLookupResult result)
    {
        var tied = result.Candidates.Take(result.TiedCandidateCount)
            .Select(static symbol => symbol.SymbolId.ToFactId())
            .ToImmutableArray();

        return new RelationResolutionOutcome(
            Handled: true,
            Method: ResolutionMethod.Candidate,
            Candidates: tied,
            UnresolvedReason: $"Ambiguous candidates for '{claim.TargetText}'.",
            Diagnostic: new RelationDiagnostic(
                AmbiguousCode,
                DiagnosticSeverity.Information,
                $"More than one candidate tied at the best rank for the observed target text of a '{claim.Kind}' relation.",
                [new DiagnosticData("relation_kind", claim.Kind), new DiagnosticData("target_text", claim.TargetText!)]));
    }

}
