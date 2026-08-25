using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Relations;

public enum UnresolvedCause
{
    AmbiguousTarget,
    NoCandidateFound,
    InsufficientEvidence,
}

public sealed record UnresolvedRecord
{
    public RelationKind Kind { get; }

    public FactReference Source { get; }

    public UnresolvedCause Cause { get; }

    public EvidenceChain Available { get; }

    public Resolution Resolution => Resolution.Unresolved;

    private UnresolvedRecord(RelationKind kind, FactReference source, UnresolvedCause cause, EvidenceChain available)
    {
        Kind = kind;
        Source = source;
        Cause = cause;
        Available = available;
    }

    public static UnresolvedRecord Create(RelationKind kind, FactReference source, UnresolvedCause? cause, EvidenceChain available)
    {
        FactGuards.RequireDefined(kind, nameof(kind));
        FactGuards.RequireInitialized(source, nameof(source));

        if (cause is null)
        {
            throw new ArgumentNullException(nameof(cause), "An unresolved record must carry its cause.");
        }

        if (!Enum.IsDefined(cause.Value))
        {
            throw new ArgumentException($"'{cause}' is not a defined value of the '{nameof(UnresolvedCause)}' axis.", nameof(cause));
        }

        if (available.DerivedFrom.IsDefault)
        {
            throw new ArgumentException("An unresolved record requires an initialized evidence chain of its available evidence.", nameof(available));
        }

        return new UnresolvedRecord(kind, source, cause.Value, available);
    }
}
