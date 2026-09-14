using Csharp2Md.Analysis.Pipeline;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineFailureDetailTests
{
    [Theory]
    [InlineData("project load failed")]
    [InlineData("Timeout")]
    public void Create_SafeDiagnosticLanguage_PreservesTheMessage(string message)
    {
        var detail = PipelineFailureDetail.Create(new InvalidOperationException(message));

        Assert.Equal($"InvalidOperationException: {message}", detail);
    }

    [Theory]
    [InlineData(@"C:\private\source\OrderHandler.cs")]
    [InlineData(@"\\server\private\source\OrderHandler.cs")]
    [InlineData("/home/private/source/OrderHandler.cs")]
    [InlineData("\"C:\\private source\\OrderHandler.cs\"")]
    [InlineData("'/home/private source/OrderHandler.cs'")]
    public void Create_MessageContainsOnlyAnAbsolutePath_ReportsOnlyTheExceptionType(string path)
    {
        var detail = PipelineFailureDetail.Create(new InvalidOperationException(path));

        Assert.Equal("InvalidOperationException", detail);
    }

    [Fact]
    public void Create_SuspectedSecret_PreservesTheSafeMessageAndRemovesTheValue()
    {
        const string secret = "pipeline-secret";

        var detail = PipelineFailureDetail.Create(
            new InvalidOperationException($"Could not authenticate; Password={secret}"));

        Assert.Equal("InvalidOperationException: Could not authenticate; Password=***", detail);
        Assert.DoesNotContain(secret, detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Password=\"very secret\"")]
    [InlineData("Password=\"it's very secret\"")]
    [InlineData("Password=very secret")]
    [InlineData("token='very secret'")]
    [InlineData("Authorization: Bearer \"very secret\"")]
    public void Create_QuotedSecretContainingSpaces_RemovesTheEntireValue(string secretAssignment)
    {
        var detail = PipelineFailureDetail.Create(
            new InvalidOperationException($"Could not authenticate; {secretAssignment}"));

        Assert.StartsWith("InvalidOperationException: Could not authenticate;", detail, StringComparison.Ordinal);
        Assert.Contains("***", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("very", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_UnlabeledRawSyntax_ReportsOnlyTheExceptionType()
    {
        var detail = PipelineFailureDetail.Create(
            new InvalidOperationException("public sealed class Secret { string Token => rawToken; }"));

        Assert.Equal("InvalidOperationException", detail);
    }

    [Theory]
    [InlineData("return customer.Password;")]
    [InlineData("public sealed class Secret")]
    [InlineData("var secret = customer.Password;")]
    [InlineData("x + secretValue")]
    [InlineData("Customer customer")]
    [InlineData("string error")]
    [InlineData("\"load failed\"")]
    [InlineData("@\"load failed\"")]
    [InlineData("$\"load failed\"")]
    [InlineData("$$\"\"\"load failed\"\"\"")]
    [InlineData("\"\"\"load failed\"\"\"")]
    [InlineData("\"load failed\"u8")]
    public void Create_UnlabeledRawSyntaxWithoutBraces_ReportsOnlyTheExceptionType(string source)
    {
        var detail = PipelineFailureDetail.Create(new InvalidOperationException(source));

        Assert.Equal("InvalidOperationException", detail);
    }

    [Fact]
    public void Create_SourceExcerptWithoutSafeMessage_ReportsOnlyTheExceptionType()
    {
        var detail = PipelineFailureDetail.Create(
            new InvalidOperationException("source: public sealed class Secret { string Token = \"raw-token\"; }"));

        Assert.Equal("InvalidOperationException", detail);
    }
}
