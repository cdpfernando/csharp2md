using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Analysis.DataAccess;

/// <summary>
/// Every claim the run collected, plus the extents pass two needs to validate their evidence.
/// </summary>
internal sealed record DatabaseClaimSnapshot(
    ImmutableArray<RawDatabaseClaim> Claims,
    ImmutableArray<DocumentExtent> Documents);

/// <summary>
/// Holds each document's claims across the run, together with the <see cref="DocumentExtent"/> of only
/// those documents that produced one. Retaining line data for silent documents would mean holding the
/// whole codebase in memory to satisfy the solution-level fragment's evidence validation.
/// </summary>
internal sealed class DatabaseClaimAccumulator
{
    private readonly List<RawDatabaseClaim> _claims = [];
    private readonly Dictionary<DocumentFactId, DocumentExtent> _documents = [];

    public void Add(
        DocumentFactId documentId,
        string relativePath,
        ImmutableArray<int> lineLengths,
        ImmutableArray<RawDatabaseClaim> claims)
    {
        if (claims.IsDefaultOrEmpty)
        {
            return;
        }

        _claims.AddRange(claims);
        _documents[documentId] = DocumentExtent.Create(documentId, relativePath, lineLengths);
    }

    /// <summary>
    /// Canonical order: evidence first, which sorts by document, path and position, then the analyzer
    /// and the claim kind. Evidence never ties across documents, so the order a document was added in
    /// cannot reach the snapshot; within one document the collector's own order is preserved by the
    /// stable sort.
    /// </summary>
    public DatabaseClaimSnapshot ToSnapshot() => new(
        _claims
            .OrderBy(static claim => claim.Evidence)
            .ThenBy(static claim => claim.AnalyzerId.Value, StringComparer.Ordinal)
            .ThenBy(static claim => claim.Kind)
            .ToImmutableArray(),
        _documents.Values
            .OrderBy(static document => document.DocumentId.Value, StringComparer.Ordinal)
            .ToImmutableArray());
}
