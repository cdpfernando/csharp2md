using Csharp2Md.Core.Publication.Safety;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class PublicationSafetyScannerTests
{
    [Theory]
    [Trait("Requirement", "PKG-07")]
    [InlineData("relative/file.cs")]
    [InlineData("key=value")]
    [InlineData("hello world")]
    public void ScanStructuredValue_RetainsSafeValues(string value) =>
        Assert.Equal(PublicationSafetyDisposition.Retained, PublicationSafetyScanner.ScanStructuredValue(value).Disposition);

    [Theory]
    [Trait("Requirement", "PUB-06")]
    [InlineData("C:/secrets/config.json")]
    [InlineData("\\\\server\\share\\secret.txt")]
    [InlineData("../outside.json")]
    [InlineData("folder\\file.json")]
    public void ScanStructuredValue_RedactsOrRejectsUnsafePaths(string value)
    {
        var result = PublicationSafetyScanner.ScanStructuredValue(value);
        Assert.NotEqual(PublicationSafetyDisposition.Retained, result.Disposition);
        Assert.DoesNotContain(value, result.Value, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Requirement", "PUB-07")]
    [InlineData("password=hunter2")]
    [InlineData("token: abc")]
    [InlineData("ApiKey=abc")]
    [InlineData("secret = abc")]
    public void ScanStructuredValue_RedactsSecretsWithoutLeakingThem(string value)
    {
        var result = PublicationSafetyScanner.ScanStructuredValue(value);
        Assert.Equal(PublicationSafetyDisposition.Redacted, result.Disposition);
        Assert.Equal("[REDACTED]", result.Value);
        Assert.DoesNotContain("abc", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CRT-08")]
    public void RedactCSharpSource_DoesNotClassifyCommentTriviaAsPath()
    {
        var source = "// C:/not/a/path\\nvar value = \"safe\";";
        var result = PublicationSafetyScanner.RedactCSharpSource(source);
        Assert.Equal(PublicationSafetyDisposition.Retained, result.Disposition);
        Assert.Equal(source, result.Value);
    }

    [Theory]
    [Trait("Requirement", "PUB-06")]
    [InlineData("var value = \"C:/secret.txt\";", "C:/secret.txt")]
    [InlineData("var value = \"password=abc\";", "password=abc")]
    [InlineData("var value = \"// harmless text\";", "// harmless text")]
    public void RedactCSharpSource_OnlyRedactsUnsafeLiteralValues(string source, string prohibited)
    {
        var result = PublicationSafetyScanner.RedactCSharpSource(source);
        Assert.Equal(PublicationSafetyDisposition.Redacted, result.Disposition);
        Assert.DoesNotContain(prohibited, result.Value, StringComparison.Ordinal);
        Assert.Contains("\"[REDACTED]\"", result.Value, StringComparison.Ordinal);
    }
}
