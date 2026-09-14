using Csharp2Md.Analysis.Pipeline;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineFailureDetailTests
{
    [Theory]
    [InlineData("project load failed")]
    [InlineData("Timeout")]
    [Trait("Requirement", "APR-02")]
    public void Create_SafeDiagnosticLanguage_PreservesTheMessage(string message)
    {
        var detail = PipelineFailureDetail.Create(new InvalidOperationException(message));

        Assert.Equal($"InvalidOperationException: {message}", detail);
        Assert.DoesNotContain('\r', detail);
        Assert.DoesNotContain('\n', detail);
    }

    [Theory]
    [InlineData(@"C:\private\source\OrderHandler.cs")]
    [InlineData(@"\\server\private\source\OrderHandler.cs")]
    [InlineData("/home/private/source/OrderHandler.cs")]
    [InlineData("\"C:\\private source\\OrderHandler.cs\"")]
    [InlineData("'/home/private source/OrderHandler.cs'")]
    [Trait("Requirement", "APR-03")]
    public void Create_MessageContainsOnlyAnAbsolutePath_ReportsOnlyTheExceptionType(string path)
    {
        var detail = PipelineFailureDetail.Create(new InvalidOperationException(path));

        Assert.Equal("InvalidOperationException", detail);
    }

    [Fact]
    [Trait("Requirement", "APR-03")]
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
    [Trait("Requirement", "APR-03")]
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
    [Trait("Requirement", "APR-03")]
    [Trait("Requirement", "APR-05")]
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
    [Trait("Requirement", "APR-03")]
    [Trait("Requirement", "APR-05")]
    public void Create_UnlabeledRawSyntaxWithoutBraces_ReportsOnlyTheExceptionType(string source)
    {
        var detail = PipelineFailureDetail.Create(new InvalidOperationException(source));

        Assert.Equal("InvalidOperationException", detail);
    }

    [Fact]
    [Trait("Requirement", "APR-03")]
    [Trait("Requirement", "APR-05")]
    public void Create_SourceExcerptWithoutSafeMessage_ReportsOnlyTheExceptionType()
    {
        var detail = PipelineFailureDetail.Create(
            new InvalidOperationException("source: public sealed class Secret { string Token = \"raw-token\"; }"));

        Assert.Equal("InvalidOperationException", detail);
    }

    [Fact]
    [Trait("Requirement", "APR-02")]
    [Trait("Requirement", "APR-05")]
    public void Create_ExceptionWithStackTrace_EmitsOneLineWithoutStackFrames()
    {
        InvalidOperationException caught;
        try
        {
            throw new InvalidOperationException("project load failed");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        var detail = PipelineFailureDetail.Create(caught);

        Assert.Equal("InvalidOperationException: project load failed", detail);
        Assert.DoesNotContain('\r', detail);
        Assert.DoesNotContain('\n', detail);
        Assert.False(string.IsNullOrEmpty(caught.StackTrace));
        Assert.DoesNotContain(caught.StackTrace, detail, StringComparison.Ordinal);
    }
}
