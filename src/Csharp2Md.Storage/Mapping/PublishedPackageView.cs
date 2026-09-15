using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

public readonly record struct ArtifactSlot(string CanonicalKey, ArtifactRole Role, int Count);

public readonly record struct ArtifactCitation(string ArtifactKey, int Ordinal);

/// <summary>
/// A read-only view over one <see cref="LayoutPlan"/>: <see cref="TryLocate"/> and
/// <see cref="TryLocateRelation"/> resolve a fact id or a relation's original position to the shard-aware
/// citation the plan assigned it, whether or not that family was split (AD-023, GCPC-041).
/// </summary>
public sealed class PublishedPackageView
{
    public WireDocument Document { get; }

    public LayoutPlan Plan { get; }

    public ImmutableArray<ArtifactSlot> Slots => Plan.Slots;

    private PublishedPackageView(WireDocument document, LayoutPlan plan)
    {
        Document = document;
        Plan = plan;
    }

    /// <summary>
    /// Builds the view using an unsplit plan (an effectively unbounded ceiling), preserving the shape
    /// every caller of this overload already depends on.
    /// </summary>
    public static PublishedPackageView From(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return From(document, LayoutPlanner.Plan(document, int.MaxValue));
    }

    /// <summary>Builds the view over an already-computed plan, so a writer and its citations always agree.</summary>
    public static PublishedPackageView From(WireDocument document, LayoutPlan plan)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(plan);
        return new PublishedPackageView(document, plan);
    }

    public bool TryLocate(string factId, out ArtifactCitation citation)
    {
        if (string.IsNullOrEmpty(factId))
        {
            citation = default;
            return false;
        }

        return Plan.FactLocations.TryGetValue(factId, out citation);
    }

    public bool TryLocateRelation(string kind, int index, out ArtifactCitation citation)
    {
        citation = default;
        if (string.IsNullOrEmpty(kind) || index < 0)
        {
            return false;
        }

        if (!Plan.RelationLocations.TryGetValue(kind, out var citations) || index >= citations.Length)
        {
            return false;
        }

        citation = citations[index];
        return true;
    }

    /// <summary>Locates the citation for the record at <paramref name="index"/> in the document's own
    /// <c>Candidates</c> array, however the family was split (GCPC-040, GCPC-041).</summary>
    public bool TryLocateCandidate(int index, out ArtifactCitation citation) =>
        TryLocateIndexed(Plan.CandidateLocations, index, out citation);

    /// <summary>Locates the citation for the record at <paramref name="index"/> in the document's own
    /// <c>Unresolved</c> array, however the family was split (GCPC-040, GCPC-041).</summary>
    public bool TryLocateUnresolved(int index, out ArtifactCitation citation) =>
        TryLocateIndexed(Plan.UnresolvedLocations, index, out citation);

    /// <summary>Locates the citation for the record at <paramref name="index"/> in the document's own
    /// <c>Frontiers</c> array, however the family was split (GCPC-040, GCPC-041).</summary>
    public bool TryLocateFrontier(int index, out ArtifactCitation citation) =>
        TryLocateIndexed(Plan.FrontierLocations, index, out citation);

    private static bool TryLocateIndexed(ImmutableArray<ArtifactCitation> citations, int index, out ArtifactCitation citation)
    {
        citation = default;
        if (index < 0 || citations.IsDefault || index >= citations.Length)
        {
            return false;
        }

        citation = citations[index];
        return true;
    }
}
