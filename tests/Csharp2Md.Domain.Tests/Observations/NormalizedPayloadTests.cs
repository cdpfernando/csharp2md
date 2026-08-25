using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Domain.Tests.Observations;

public sealed class NormalizedPayloadTests
{
    [Fact]
    [Trait("Requirement", "TAX-35")]
    public void Create_TwoShuffledInputs_ProduceOneEqualPayload()
    {
        var route = StructuralLiteral.Create(LiteralRole.Route, "/orders", "route");
        var channel = StructuralLiteral.Create(LiteralRole.Channel, "orders.created", "channel");

        var forward = NormalizedPayload.Create([new PayloadEntry("b-route", route), new PayloadEntry("a-channel", channel)]);
        var reverse = NormalizedPayload.Create([new PayloadEntry("a-channel", channel), new PayloadEntry("b-route", route)]);

        Assert.Equal(forward, reverse);
        Assert.Equal(["a-channel", "b-route"], forward.Entries.Select(e => e.Key).ToArray());
    }

    [Fact]
    [Trait("Requirement", "TAX-35")]
    public void PayloadEntry_ValuePropertyType_IsStructuralLiteral()
    {
        var property = typeof(PayloadEntry).GetProperty(nameof(PayloadEntry.Value));

        Assert.NotNull(property);
        Assert.Equal(typeof(StructuralLiteral), property!.PropertyType);
    }

    [Fact]
    [Trait("Requirement", "TAX-35")]
    public void Create_DuplicateKey_IsRejectedNamingTheKey()
    {
        var route = StructuralLiteral.Create(LiteralRole.Route, "/orders", "route");
        var channel = StructuralLiteral.Create(LiteralRole.Channel, "orders.created", "channel");

        var exception = Assert.Throws<ArgumentException>(
            () => NormalizedPayload.Create([new PayloadEntry("same", route), new PayloadEntry("same", channel)]));

        Assert.Contains("same", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-35")]
    public void Create_EmptyPayload_IsAccepted()
    {
        var payload = NormalizedPayload.Create([]);

        Assert.Empty(payload.Entries);
    }

    [Theory]
    [Trait("Requirement", "TAX-35")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad  key")]
    public void PayloadEntry_NonCanonicalKey_IsRejected(string key)
    {
        var literal = StructuralLiteral.Create(LiteralRole.Route, "/orders", "route");

        Assert.Throws<ArgumentException>(() => new PayloadEntry(key, literal));
    }
}
