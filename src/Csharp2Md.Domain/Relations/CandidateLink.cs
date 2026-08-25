using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Relations;

public sealed record CandidateLink
{
    public RelationKind Kind { get; }

    public FactReference Source { get; }

    public FactReference ProposedTarget { get; }

    public EvidenceChain DerivedFrom { get; }

    public Resolution Resolution => Resolution.Candidate;

    private CandidateLink(RelationKind kind, FactReference source, FactReference proposedTarget, EvidenceChain derivedFrom)
    {
        Kind = kind;
        Source = source;
        ProposedTarget = proposedTarget;
        DerivedFrom = derivedFrom;
    }

    public static CandidateLink Create(RelationKind kind, FactReference source, FactReference proposedTarget, EvidenceChain derivedFrom)
    {
        FactGuards.RequireDefined(kind, nameof(kind));
        FactGuards.RequireInitialized(source, nameof(source));
        FactGuards.RequireInitialized(proposedTarget, nameof(proposedTarget));

        if (derivedFrom.DerivedFrom.IsDefault)
        {
            throw new ArgumentException("A candidate link requires an initialized evidence chain.", nameof(derivedFrom));
        }

        return new CandidateLink(kind, source, proposedTarget, derivedFrom);
    }
}
