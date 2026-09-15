using Csharp2Md.Core.PackageBuilding.Identity;
namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class PublicIdRegistryTests
{
    [Theory]
    [InlineData("entity:orders")]
    [InlineData("entity:payments")]
    [InlineData("relation:uses")]
    [InlineData("evidence:source")]
    [InlineData("document:orders.cs")]
    [InlineData("variant:net10.0")]
    [InlineData("component:orders")]
    [InlineData("deployment:api")]
    [InlineData("contract:order")]
    [InlineData("persistence:postgres")]
    [InlineData("gap:unknown")]
    [InlineData("root:application")]
    [Trait("Requirement", "STO-01")]
    public void Register_ProducesTheSpecifiedPublicIdGrammar(string category)
    {
        var id = new PublicIdRegistry().Register("ent", category);
        Assert.Matches("^[a-z]{3}_[0-9a-v]{16}$", id);
    }

    [Fact]
    [Trait("Requirement", "STO-01")]
    public void Register_IsDeterministicAndUsesItsKindPrefix()
    {
        Assert.Equal(new PublicIdRegistry().Register("rel", "relation:uses"), new PublicIdRegistry().Register("rel", "relation:uses"));
        Assert.StartsWith("rel_", new PublicIdRegistry().Register("rel", "relation:uses"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STO-02")]
    public void Register_RejectsForcedDigestCollisionWithBothCategories()
    {
        var registry = new PublicIdRegistry();
        var first = registry.Register("ent", "entity:first");
        var categories = typeof(PublicIdRegistry).GetField("categories", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(registry) as Dictionary<string, string>;
        categories![first] = "entity:other";
        var exception = Assert.Throws<PublicIdCollisionException>(() => registry.Register("ent", "entity:first"));
        Assert.Equal(first, exception.Id);
        Assert.Equal("entity:other", exception.FirstCategory);
        Assert.Equal("entity:first", exception.SecondCategory);
        Assert.DoesNotContain(":\\", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ENt")]
    [InlineData("en")]
    [InlineData("enty")]
    [Trait("Requirement", "STO-01")]
    public void Register_RejectsNonCanonicalPrefixes(string prefix) =>
        Assert.Throws<ArgumentException>(() => new PublicIdRegistry().Register(prefix, "entity:orders"));
}
