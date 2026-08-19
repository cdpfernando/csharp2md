using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Topic;

public sealed class TopicLayoutTests
{
    [Fact]
    public void RawRoot_ReturnsRawBeneathOutputRoot()
    {
        var outputRoot = Path.Combine("some", "arbitrary", "output-root");

        var rawRoot = TopicLayout.RawRoot(outputRoot);

        Assert.Equal(Path.Combine(outputRoot, "raw"), rawRoot);
    }

    [Fact]
    public void CodebaseRoot_ReturnsCodebaseBeneathRawRoot()
    {
        var outputRoot = Path.Combine("some", "arbitrary", "output-root");

        var codebaseRoot = TopicLayout.CodebaseRoot(outputRoot);

        Assert.Equal(Path.Combine(outputRoot, "raw", "codebase"), codebaseRoot);
        Assert.Equal(Path.Combine(TopicLayout.RawRoot(outputRoot), "codebase"), codebaseRoot);
    }

    [Fact]
    public void ServiceRoot_NestsUnderRawCodebase()
    {
        var outputRoot = Path.Combine("some", "arbitrary", "output-root");
        var service = new ServiceName("Acme.Orders");

        var serviceRoot = TopicLayout.ServiceRoot(outputRoot, service);

        Assert.Equal(Path.Combine(outputRoot, "raw", "codebase", "Acme.Orders"), serviceRoot);
        Assert.Equal(Path.Combine(TopicLayout.CodebaseRoot(outputRoot), "Acme.Orders"), serviceRoot);
        Assert.StartsWith(TopicLayout.CodebaseRoot(outputRoot), serviceRoot, StringComparison.Ordinal);
    }
}
