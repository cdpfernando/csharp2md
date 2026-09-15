using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Classification;

/// <summary>
/// Every disposition a recognized invocation occurrence can carry (GCPC-011). Exactly one of these is
/// expected per occurrence -- <see cref="InvocationDispositionLedger"/> is what makes a second one
/// detectable instead of silently overwriting the first.
/// </summary>
internal enum InvocationDispositionKind
{
    Confirmed,
    Candidate,
    Unresolved,
    OpenFrontier,
    Excluded,
}

/// <summary>
/// The declared reason an invocation occurrence is a counted exclusion rather than a relation,
/// candidate or unresolved record (GCPC-016). Named after the branches <c>InvokesPass</c> excludes
/// today: a call that binds outside the analyzed solution scope, and a duplicate edge the pass has
/// already recorded for the same occurrence.
/// </summary>
internal enum InvocationExclusionCategory
{
    ExternalFrameworkCallable,
    DuplicateEdge,
}

/// <summary>
/// One explicit disposition for one recognized invocation occurrence.
/// </summary>
internal sealed record InvocationDisposition
{
    public ObservationIdentity Occurrence { get; }

    public InvocationDispositionKind Kind { get; }

    public InvocationExclusionCategory? ExclusionCategory { get; }

    private InvocationDisposition(
        ObservationIdentity occurrence, InvocationDispositionKind kind, InvocationExclusionCategory? exclusionCategory)
    {
        Occurrence = occurrence;
        Kind = kind;
        ExclusionCategory = exclusionCategory;
    }

    public static InvocationDisposition Create(ObservationIdentity occurrence, InvocationDispositionKind kind)
    {
        RequireDefinedKind(kind);
        if (kind is InvocationDispositionKind.Excluded)
        {
            throw new ArgumentException(
                "An excluded disposition must declare its exclusion category; use the Create overload that takes one.",
                nameof(kind));
        }

        return new InvocationDisposition(occurrence, kind, exclusionCategory: null);
    }

    public static InvocationDisposition Create(ObservationIdentity occurrence, InvocationExclusionCategory exclusionCategory)
    {
        if (!Enum.IsDefined(exclusionCategory))
        {
            throw new ArgumentOutOfRangeException(
                nameof(exclusionCategory),
                exclusionCategory,
                $"'{exclusionCategory}' is not a defined value of the '{nameof(InvocationExclusionCategory)}' axis.");
        }

        return new InvocationDisposition(occurrence, InvocationDispositionKind.Excluded, exclusionCategory);
    }

    private static void RequireDefinedKind(InvocationDispositionKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind), kind, $"'{kind}' is not a defined value of the '{nameof(InvocationDispositionKind)}' axis.");
        }
    }
}

/// <summary>
/// Accumulates one <see cref="InvocationDisposition"/> per recognized invocation occurrence, mirroring
/// <c>SnapshotAccumulator</c>'s add-and-collect shape. Unlike a plain list, a second disposition for an
/// occurrence that already has one is never silently accepted in its place: it is recorded as a
/// duplicate instead, so GCPC-011's "exactly one disposition" invariant has something to check against.
/// </summary>
internal sealed class InvocationDispositionLedger
{
    private readonly Dictionary<ObservationIdentity, InvocationDisposition> _byOccurrence = [];
    private readonly List<InvocationDisposition> _duplicates = [];

    public IReadOnlyCollection<InvocationDisposition> Dispositions => _byOccurrence.Values;

    /// <summary>
    /// Dispositions that arrived for an occurrence that already had one. Non-empty here means GCPC-011
    /// was violated for at least one occurrence -- the first-recorded disposition is kept, and every
    /// later one for the same occurrence lands here instead of overwriting it.
    /// </summary>
    public IReadOnlyCollection<InvocationDisposition> Duplicates => _duplicates;

    public void Add(InvocationDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(disposition);
        if (!_byOccurrence.TryAdd(disposition.Occurrence, disposition))
        {
            _duplicates.Add(disposition);
        }
    }
}
