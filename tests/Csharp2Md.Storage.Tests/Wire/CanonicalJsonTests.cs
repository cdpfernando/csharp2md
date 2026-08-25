using System.Text;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Wire;

public sealed class CanonicalJsonTests
{
    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Write_OmitsUtf8Bom()
    {
        var bytes = CanonicalJson.Write(new SampleDto("alpha", 1, new NestedDto("beta")));

        Assert.False(bytes.IsDefaultOrEmpty);
        Assert.NotEqual((byte)0xEF, bytes[0]);
    }

    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Write_UsesLineFeedNewlinesOnly()
    {
        var bytes = CanonicalJson.Write(new SampleDto("alpha", 1, new NestedDto("beta")));

        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Contains((byte)'\n', bytes);
    }

    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Write_IndentsWithTwoSpaces()
    {
        var bytes = CanonicalJson.Write(new SampleDto("alpha", 1, new NestedDto("beta")));
        var json = Encoding.UTF8.GetString(bytes.AsSpan());

        Assert.Contains("\n  \"Name\"", json, StringComparison.Ordinal);
        Assert.Contains("\n    \"Label\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\t", json, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-44")]
    public void Read_RoundTripsDtoValues()
    {
        var original = new SampleDto("alpha", 7, new NestedDto("beta"));

        var restored = CanonicalJson.Read<SampleDto>(CanonicalJson.Write(original).AsSpan());

        Assert.Equal(original, restored);
    }

    public sealed record NestedDto(string Label);

    public sealed record SampleDto(string Name, int Count, NestedDto Nested);
}
