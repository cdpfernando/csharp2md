using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class UnresolvedRecordTests
{
    private static FactReference SymbolReference(string name) =>
        new(new FactId("symbol", $"id1:symbol;name={name}"), "Symbol");

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create([
            new ObservationIdentity(SymbolReference("Charge"), ObservationKind.Invocation, NormalizedPayload.Create([]), 1),
        ]);

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Create_NullCause_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => UnresolvedRecord.Create(
            RelationKind.Invokes, SymbolReference("Charge"), null, ValidEvidence()));

        Assert.Equal("cause", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Create_UndefinedCause_IsRejected() =>
        Assert.Throws<ArgumentException>(() => UnresolvedRecord.Create(
            RelationKind.Invokes, SymbolReference("Charge"), (UnresolvedCause)(-1), ValidEvidence()));

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Create_CarriesTheAvailableEvidence_AndDoesNotRequireATargetIdentity()
    {
        var evidence = ValidEvidence();

        var unresolved = UnresolvedRecord.Create(RelationKind.Invokes, SymbolReference("Charge"), UnresolvedCause.AmbiguousTarget, evidence);

        Assert.Equal(evidence, unresolved.Available);
        Assert.DoesNotContain(
            typeof(UnresolvedRecord).GetProperties(),
            property => property.Name.Contains("Target", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Create_DefaultEvidenceChain_IsRejectedNamingAvailable()
    {
        var exception = Assert.Throws<ArgumentException>(() => UnresolvedRecord.Create(
            RelationKind.Invokes, SymbolReference("Charge"), UnresolvedCause.NoCandidateFound, default));

        Assert.Equal("available", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-64")]
    public void Resolution_ReadsUnresolved_AndHasNoSetter()
    {
        var unresolved = UnresolvedRecord.Create(
            RelationKind.Invokes, SymbolReference("Charge"), UnresolvedCause.InsufficientEvidence, ValidEvidence());

        Assert.Equal(Resolution.Unresolved, unresolved.Resolution);
        Assert.Null(typeof(UnresolvedRecord).GetProperty(nameof(UnresolvedRecord.Resolution))!.GetSetMethod(nonPublic: true));
    }
}
