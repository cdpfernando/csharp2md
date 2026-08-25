using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Tests.Proof;

public sealed class EvidenceChainTests
{
    private static ObservationIdentity Identity(string ownerName, int ordinal) =>
        new(
            new FactReference(FactIdGrammar.Create("widget", ("name", ownerName)), "widget"),
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            ordinal);

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_EmptyChain_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentException>(() => EvidenceChain.Create([]));

        Assert.Equal("derivedFrom", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_DuplicateIdentities_CollapseToOneEntry()
    {
        var identity = Identity("a", 1);

        var chain = EvidenceChain.Create([identity, identity, identity]);

        Assert.Single(chain.DerivedFrom);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_ShuffledInputs_ProduceEqualChainsInADeterministicOrder()
    {
        var first = Identity("a", 1);
        var second = Identity("b", 1);
        var third = Identity("c", 1);

        var forward = EvidenceChain.Create([first, second, third]);
        var shuffled = EvidenceChain.Create([third, first, second]);

        Assert.Equal(forward, shuffled);
        Assert.Equal(new[] { first, second, third }, forward.DerivedFrom.ToArray());
    }
}
