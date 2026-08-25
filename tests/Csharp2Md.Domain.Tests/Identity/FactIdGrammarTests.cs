using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class FactIdGrammarTests
{
    [Theory]
    [Trait("Requirement", "TAX-68")]
    [InlineData("/repo/App.csproj")]
    [InlineData("C:/repo/App.csproj")]
    [InlineData("src\\App.csproj")]
    [InlineData("src/./App.csproj")]
    [InlineData("src/../App.csproj")]
    public void ValidateRelativePath_AbsoluteRootedBackslashOrDotSegmentPath_IsRejectedNamingParameter(string path)
    {
        var exception = Assert.Throws<ArgumentException>(() => FactIdGrammar.ValidateRelativePath(path, "path"));

        Assert.Equal("path", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-69")]
    public void Create_OnlyEmitsDeclaredKeyValueComponents_NoImplicitLocationLabelOrTimestamp()
    {
        var id = FactIdGrammar.Create("widget", ("name", "value"));

        Assert.Equal("id1:widget;name=value", id.Value);
        Assert.DoesNotContain("location", id.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("label", id.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("timestamp", id.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("line", id.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Requirement", "TAX-69")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("double  space")]
    [InlineData("line\nbreak")]
    public void RequireCanonicalText_WhitespaceOnlyOrNonCanonicalValue_IsRejectedNamingParameter(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => FactIdGrammar.RequireCanonicalText(value, "component"));

        Assert.Equal("component", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-76")]
    public void Create_BlankRequiredComponent_ThrowsNamingTheMissingComponent()
    {
        var exception = Assert.Throws<ArgumentException>(() => FactIdGrammar.Create("widget", ("name", "")));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-76")]
    public void Create_NullValue_EncodesAsSentinelWithoutThrowing()
    {
        var id = FactIdGrammar.Create("widget", ("name", "value"), ("optional", (string?)null));

        Assert.Equal("id1:widget;name=value;optional=-", id.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-68")]
    public void Create_PercentEncoding_UsesUppercaseHexAndPreservesUnreservedCharacters()
    {
        var id = FactIdGrammar.Create("widget", ("path", "src/Café.csproj"));

        Assert.Equal("id1:widget;path=src%2FCaf%C3%A9.csproj", id.Value);
    }

    [Fact]
    public void UninitializedFactId_Value_ThrowsInvalidOperationException()
    {
        var uninitialized = default(FactId);

        Assert.Throws<InvalidOperationException>(() => uninitialized.Value);
    }
}
