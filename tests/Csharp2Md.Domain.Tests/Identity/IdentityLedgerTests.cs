using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class IdentityLedgerTests
{
    [Fact]
    [Trait("Requirement", "TAX-77")]
    public void Register_SameReferenceTwice_IsIdempotent()
    {
        var ledger = new IdentityLedger();
        var reference = new FactReference(FactIdGrammar.Create("widget", ("name", "value")), "widget");

        ledger.Register(reference);
        ledger.Register(reference);

        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    [Trait("Requirement", "TAX-77")]
    public void Register_TwoDistinctFactTypesOnSameIdentity_ThrowsNamingBoth()
    {
        var ledger = new IdentityLedger();
        var id = FactIdGrammar.Create("widget", ("name", "value"));
        ledger.Register(new FactReference(id, "widget"));

        var exception = Assert.Throws<ArgumentException>(() => ledger.Register(new FactReference(id, "gadget")));

        Assert.Contains("widget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("gadget", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-77")]
    public void Register_IdentitiesDifferingOnlyByCase_AreDistinct()
    {
        var ledger = new IdentityLedger();

        ledger.Register(new FactReference(FactIdGrammar.Create("Widget", ("name", "value")), "widget"));
        ledger.Register(new FactReference(FactIdGrammar.Create("widget", ("name", "value")), "widget"));

        Assert.Equal(2, ledger.Count);
    }

    [Fact]
    [Trait("Requirement", "TAX-77")]
    public void Count_ReflectsDistinctRegisteredIdentities()
    {
        var ledger = new IdentityLedger();

        ledger.Register(new FactReference(FactIdGrammar.Create("widget", ("name", "a")), "widget"));
        ledger.Register(new FactReference(FactIdGrammar.Create("widget", ("name", "b")), "widget"));
        ledger.Register(new FactReference(FactIdGrammar.Create("widget", ("name", "a")), "widget"));

        Assert.Equal(2, ledger.Count);
    }
}
