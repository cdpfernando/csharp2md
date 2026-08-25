using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class FactReferenceTests
{
    [Fact]
    [Trait("Requirement", "TAX-77")]
    public void FactReference_CarriesIdentityAndFactType()
    {
        var id = FactIdGrammar.Create("widget", ("name", "value"));

        var reference = new FactReference(id, "widget");

        Assert.Equal(id, reference.Id);
        Assert.Equal("widget", reference.FactType);
    }

    [Theory]
    [Trait("Requirement", "TAX-77")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("wid  get")]
    [InlineData(" widget")]
    public void FactReference_NonCanonicalFactType_IsRejected(string factType)
    {
        var id = FactIdGrammar.Create("widget", ("name", "value"));

        Assert.Throws<ArgumentException>(() => new FactReference(id, factType));
    }
}
