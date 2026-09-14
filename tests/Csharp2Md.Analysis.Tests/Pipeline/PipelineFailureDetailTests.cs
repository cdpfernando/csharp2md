using Csharp2Md.Analysis.Pipeline;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineFailureDetailTests
{
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

    [Fact]
    public void Create_UnlabeledRawSyntax_ReportsOnlyTheExceptionType()
    {
        var detail = PipelineFailureDetail.Create(
            new InvalidOperationException("public sealed class Secret { string Token => rawToken; }"));

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
