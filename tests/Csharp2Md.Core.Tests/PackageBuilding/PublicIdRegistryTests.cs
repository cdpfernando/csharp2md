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

    // The grammar and determinism cases above hold for any digest, any bit slice and any 32-character
    // alphabet, so on their own they cannot tell a correct derivation from a wrong one. These two pin the
    // derivation STO-01 specifies: SHA-256 of the canonical category, the leading 80 bits, lowercase
    // base32hex. The expectations were computed outside this codebase and are written here as literals, so
    // nothing in the assertion re-uses the production code that is under test.
    [Theory]
    [InlineData("component:orders", "i0k15e1lv4fo5h8n")]
    [InlineData("solution:app", "57qlrjjre54j61bv")]
    [Trait("Requirement", "STO-01")]
    public void Register_DerivesTheIdentityFromTheLeading80DigestBits(string category, string expected) =>
        Assert.Equal("ent_" + expected, new PublicIdRegistry().Register("ent", category));

    // A second, independent check of the same two values: recomputed in the test from System.Security's
    // SHA-256 and a locally written base32hex, so a change to the production encoder is caught even if the
    // literals above were ever regenerated from it by mistake.
    [Fact]
    [Trait("Requirement", "STO-01")]
    public void Register_MatchesAnIndependentlyComputedDerivation()
    {
        const string category = "component:orders";
        var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(category));
        var value = System.Numerics.BigInteger.Zero;
        foreach (var octet in digest.AsSpan(0, 10))
        {
            value = (value << 8) | octet;
        }

        const string alphabet = "0123456789abcdefghijklmnopqrstuv";
        var expected = new char[16];
        for (var index = 15; index >= 0; index--)
        {
            expected[index] = alphabet[(int)(value & 31)];
            value >>= 5;
        }

        Assert.Equal("ent_" + new string(expected), new PublicIdRegistry().Register("ent", category));
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
