using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class CandidateLinkTests
{
    private static FactReference SymbolReference(string name) =>
        new(new FactId("symbol", $"id1:symbol;name={name}"), "Symbol");

    private static FactReference DataObjectReference(string name) =>
        new(new FactId("data-object", $"id1:data-object;name={name}"), "DataObject");

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create([
            new ObservationIdentity(SymbolReference("Charge"), ObservationKind.Invocation, NormalizedPayload.Create([]), 1),
        ]);

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Create_ProposedTarget_IsCarriedOnTheCandidate()
    {
        var target = DataObjectReference("orders");

        var candidate = CandidateLink.Create(RelationKind.MapsTo, SymbolReference("OrderEntity"), target, ValidEvidence());

        Assert.Equal(target, candidate.ProposedTarget);
    }

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Create_NoProposedTarget_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentException>(() => CandidateLink.Create(
            RelationKind.MapsTo, SymbolReference("OrderEntity"), default, ValidEvidence()));

        Assert.Equal("proposedTarget", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Resolution_ReadsCandidate_AndHasNoSetter()
    {
        var candidate = CandidateLink.Create(RelationKind.MapsTo, SymbolReference("OrderEntity"), DataObjectReference("orders"), ValidEvidence());

        Assert.Equal(Resolution.Candidate, candidate.Resolution);
        Assert.Null(typeof(CandidateLink).GetProperty(nameof(CandidateLink.Resolution))!.GetSetMethod(nonPublic: true));
    }

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Create_DefaultEvidenceChain_IsRejectedNamingDerivedFrom()
    {
        var exception = Assert.Throws<ArgumentException>(() => CandidateLink.Create(
            RelationKind.MapsTo, SymbolReference("OrderEntity"), DataObjectReference("orders"), default));

        Assert.Equal("derivedFrom", exception.ParamName);
    }
}
