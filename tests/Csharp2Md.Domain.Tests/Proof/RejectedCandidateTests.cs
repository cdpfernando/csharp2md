using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Tests.Proof;

public sealed class RejectedCandidateTests
{
    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create([
            new ObservationIdentity(
                new FactReference(FactIdGrammar.Create("widget", ("name", "value")), "widget"),
                ObservationKind.Invocation,
                NormalizedPayload.Create([]),
                1),
        ]);

    [Fact]
    [Trait("Requirement", "TAX-66")]
    public void Create_NullCause_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => RejectedCandidate.Create(ValidEvidence(), null));

        Assert.Equal("cause", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-66")]
    public void Create_UndefinedCause_IsRejected() =>
        Assert.Throws<ArgumentException>(() => RejectedCandidate.Create(ValidEvidence(), (RejectionCause)(-1)));

    [Fact]
    [Trait("Requirement", "TAX-66")]
    public void Create_ValidCause_CarriesTheEvidenceThatWasAvailable()
    {
        var evidence = ValidEvidence();

        var candidate = RejectedCandidate.Create(evidence, RejectionCause.InsufficientEvidence);

        Assert.Equal(evidence, candidate.Evidence);
        Assert.Equal(RejectionCause.InsufficientEvidence, candidate.Cause);
    }
}
