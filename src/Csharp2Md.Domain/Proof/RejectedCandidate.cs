namespace Csharp2Md.Domain.Proof;

public enum RejectionCause
{
    InsufficientEvidence,
    AmbiguousTarget,
    UnregisteredTriple,
    MissingIdentityComponent,
}

public readonly record struct RejectedCandidate
{
    public EvidenceChain Evidence { get; }

    public RejectionCause Cause { get; }

    private RejectedCandidate(EvidenceChain evidence, RejectionCause cause)
    {
        Evidence = evidence;
        Cause = cause;
    }

    public static RejectedCandidate Create(EvidenceChain evidence, RejectionCause? cause)
    {
        if (cause is null)
        {
            throw new ArgumentNullException(nameof(cause), "A rejected candidate must carry its rejection cause.");
        }

        if (!Enum.IsDefined(cause.Value))
        {
            throw new ArgumentException($"'{cause}' is not a defined value of the '{nameof(RejectionCause)}' axis.", nameof(cause));
        }

        return new RejectedCandidate(evidence, cause.Value);
    }
}
