using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Analysis.Relations;

/// <summary>
/// Every claim the run collected, plus the extents pass two needs to validate their evidence.
/// Canonical order: <see cref="RawRelation.Evidence"/> first, which sorts by document, path and
/// position, then the relation kind, then the details fingerprint - so the order documents were
/// analysed in cannot reach the snapshot (RELR-21).
/// </summary>
internal sealed record RelationClaimSnapshot(
    ImmutableArray<RawRelation> Claims,
    ImmutableArray<DocumentExtent> Documents);

/// <summary>
/// Holds each document's claims across the run, together with the <see cref="DocumentExtent"/> of only
/// those documents that produced one. Retaining line data for silent documents would mean holding the
/// whole codebase in memory to satisfy the solution-level fragment's evidence validation. Copies
/// <c>DatabaseClaimAccumulator</c> (Analysis/DataAccess/DatabaseClaimAccumulator.cs) line for line,
/// including its memory rule and ordering rationale.
/// </summary>
internal sealed class RelationClaimAccumulator
{
    private readonly List<RawRelation> _claims = [];
    private readonly Dictionary<DocumentFactId, DocumentExtent> _documents = [];

    public void Add(
        DocumentFactId documentId,
        string relativePath,
        ImmutableArray<int> lineLengths,
        ImmutableArray<RawRelation> claims)
    {
        if (claims.IsDefaultOrEmpty)
        {
            return;
        }

        _claims.AddRange(claims);
        _documents[documentId] = DocumentExtent.Create(documentId, relativePath, lineLengths);
    }

    /// <summary>
    /// Accepts already-targeted claims with no extent of their own - the database resolver's output,
    /// whose evidence documents were already recorded by the <see cref="Add"/> calls their owning
    /// documents made during pass one.
    /// </summary>
    public void AddResolved(ImmutableArray<RawRelation> claims)
    {
        if (claims.IsDefaultOrEmpty)
        {
            return;
        }

        _claims.AddRange(claims);
    }

    /// <summary>
    /// Canonical order: evidence first, which sorts by document, path and position, then the relation
    /// kind, then the details fingerprint. Evidence never ties across documents, so the order a
    /// document was added in cannot reach the snapshot; within one document the collector's own order
    /// is preserved by the stable sort.
    /// </summary>
    public RelationClaimSnapshot ToSnapshot() => new(
        _claims
            .OrderBy(static claim => claim.Evidence)
            .ThenBy(static claim => claim.Kind, StringComparer.Ordinal)
            .ThenBy(static claim => DetailsFingerprint(claim.Details), StringComparer.Ordinal)
            .ToImmutableArray(),
        _documents.Values
            .OrderBy(static document => document.DocumentId.Value, StringComparer.Ordinal)
            .ToImmutableArray());

    private static string DetailsFingerprint(ImmutableArray<RelationDetail> details) =>
        details.IsDefaultOrEmpty
            ? string.Empty
            : string.Join(
                '|',
                details
                    .OrderBy(static detail => detail.Key, StringComparer.Ordinal)
                    .ThenBy(static detail => detail.Value, StringComparer.Ordinal)
                    .Select(static detail => $"{detail.Key}={detail.Value}"));
}
