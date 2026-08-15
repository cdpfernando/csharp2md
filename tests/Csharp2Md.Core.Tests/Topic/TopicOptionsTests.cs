using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Topic;

public sealed class TopicOptionsTests
{
    [Theory]
    [InlineData("Acme Shop", "acme-shop")]
    [InlineData("  Multiple   Spaces  ", "multiple-spaces")]
    [InlineData("Already-Slugged", "already-slugged")]
    [InlineData("--leading-and-trailing--", "leading-and-trailing")]
    public void Slugify_LowercasesCollapsesNonAlphanumericRunsAndTrimsDashes(string input, string expected)
    {
        Assert.Equal(expected, TopicOptions.Slugify(input));
    }

    [Fact]
    public void Default_TopicIsSlugOfInputDirectoryName()
    {
        var options = TopicOptions.Default(Path.Combine("some", "path", "Acme Shop"));

        Assert.Equal("acme-shop", options.Topic);
    }

    [Fact]
    public void Default_DomainIsSystemDesign()
    {
        var options = TopicOptions.Default(Path.Combine("some", "path", "Acme Shop"));

        Assert.Equal("system-design", options.Domain);
    }

    [Fact]
    public void Create_AcceptsPlainSlug()
    {
        var result = TopicOptions.Create("acme-shop", null, "input-root");

        Assert.True(result.IsSuccess);
        Assert.Equal("acme-shop", result.Options!.Topic);
    }

    [Fact]
    public void Create_AcceptsGroupSlashNameSlug()
    {
        var result = TopicOptions.Create("acme-shop/orders", null, "input-root");

        Assert.True(result.IsSuccess);
        Assert.Equal("acme-shop/orders", result.Options!.Topic);
    }

    [Fact]
    public void Create_RejectsUppercaseAsErrorNotException()
    {
        var result = TopicOptions.Create("Acme-Shop", null, "input-root");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Options);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Create_RejectsLeadingDashAsErrorNotException()
    {
        var result = TopicOptions.Create("-acme-shop", null, "input-root");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Options);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Create_RejectsEmptyAsErrorNotException()
    {
        var result = TopicOptions.Create("", null, "input-root");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Options);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Create_OmittedTopicDefaultsToSlugOfInputDirectoryName()
    {
        var result = TopicOptions.Create(null, "system-design", Path.Combine("some", "path", "Acme Shop"));

        Assert.True(result.IsSuccess);
        Assert.Equal("acme-shop", result.Options!.Topic);
    }

    [Fact]
    public void Create_OmittedDomainDefaultsToSystemDesign()
    {
        var result = TopicOptions.Create("acme-shop", null, "input-root");

        Assert.True(result.IsSuccess);
        Assert.Equal("system-design", result.Options!.Domain);
    }
}
