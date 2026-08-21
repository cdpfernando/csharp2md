using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Relations.Resolution;

/// <summary>
/// Resolves a <c>calls</c> claim that carries a receiver (design.md's strategy #3, RELR-05..RELR-08):
/// once this strategy determines a claim is "calls with a receiver" it owns that claim fully - every
/// outcome below is <see cref="RelationResolutionOutcome.Handled"/>, matching AC9/AC10's dispatch-by-kind
/// contract rather than falling back to a generic name lookup on a partial failure. A <c>calls</c> claim
/// with no receiver at all (a bare or static invocation) declines, leaving it to <see cref="SymbolIndexStrategy"/>.
/// </summary>
internal sealed class ReceiverTypeStrategy : IRelationResolutionStrategy
{
    private const string UndeterminableReceiverTypeCode = "C2M-RELR-003";
    private const string NoCandidateCode = "C2M-RELR-001";
    private const string AmbiguousCode = "C2M-RELR-002";

    public RelationResolutionOutcome TryResolve(RelationResolutionContext context)
    {
        var claim = context.Relation;
        if (claim.Kind is not "calls" || string.IsNullOrEmpty(claim.ReceiverText) || claim.MemberName is null)
        {
            return RelationResolutionOutcome.None;
        }

        if (string.IsNullOrEmpty(claim.ReceiverTypeText))
        {
            return new RelationResolutionOutcome(
                Handled: true,
                Method: ResolutionMethod.Unresolved,
                UnresolvedReason: $"The declared type of receiver '{claim.ReceiverText}' could not be determined.",
                Diagnostic: new RelationDiagnostic(
                    UndeterminableReceiverTypeCode,
                    DiagnosticSeverity.Information,
                    "The receiver identifier's declared type could not be determined.",
                    [new DiagnosticData("receiver_text", claim.ReceiverText)]));
        }

        var lookup = new MethodLookup
        {
            Name = claim.MemberName,
            ReceiverType = claim.ReceiverTypeText,
            Namespace = claim.Namespace,
            ProjectId = claim.ProjectId,
            ArgumentCount = claim.ArgumentCount,
            ArgumentTypes = claim.ArgumentTypes,
            Imports = claim.Imports,
        };

        var result = context.SymbolIndex.FindMethods(lookup);
        if (result.Methods.IsEmpty)
        {
            return new RelationResolutionOutcome(
                Handled: true,
                Method: ResolutionMethod.Unresolved,
                UnresolvedReason: $"No method found for '{claim.ReceiverTypeText}.{claim.MemberName}'.",
                Diagnostic: new RelationDiagnostic(
                    NoCandidateCode,
                    DiagnosticSeverity.Information,
                    "No candidate was found for the observed target text of a 'calls' relation.",
                    [
                        new DiagnosticData("relation_kind", claim.Kind),
                        new DiagnosticData("target_text", $"{claim.ReceiverTypeText}.{claim.MemberName}"),
                    ]));
        }

        if (result.TiedCandidateCount == 1)
        {
            return new RelationResolutionOutcome(
                Handled: true,
                TargetId: result.Methods[0].SymbolId.ToFactId(),
                Method: ResolutionMethod.Syntactic);
        }

        var tied = result.Methods.Take(result.TiedCandidateCount)
            .Select(static symbol => symbol.SymbolId.ToFactId())
            .ToImmutableArray();

        return new RelationResolutionOutcome(
            Handled: true,
            Method: ResolutionMethod.Candidate,
            Candidates: tied,
            UnresolvedReason: $"Ambiguous method candidates for '{claim.ReceiverTypeText}.{claim.MemberName}'.",
            Diagnostic: new RelationDiagnostic(
                AmbiguousCode,
                DiagnosticSeverity.Information,
                "More than one method tied at the best rank for a 'calls' relation.",
                [
                    new DiagnosticData("relation_kind", claim.Kind),
                    new DiagnosticData("target_text", $"{claim.ReceiverTypeText}.{claim.MemberName}"),
                ]));
    }
}
