using Csharp2Md.Core.Topic;
using YamlDotNet.Serialization;

namespace Csharp2Md.Core.Tests.Topic;

public sealed class FrontmatterYamlTests
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    private static Frontmatter CreateFrontmatter(string title = "OrderService", IReadOnlyList<string>? tags = null) => new(
        Title: title,
        SourceKind: SourceKind.CodebaseFile,
        SourcePath: "Acme.Orders/OrderService.cs",
        Domain: "system-design",
        Topic: "acme-shop",
        FileType: FileType.Service,
        Tags: tags ?? ["async-patterns", "persistence"]);

    [Fact]
    public void Render_EmitsKeysInSchemaDeclaredOrder()
    {
        var rendered = FrontmatterYaml.Render(CreateFrontmatter());

        var keys = rendered.Split('\n')
            .Where(line => line.Contains(':', StringComparison.Ordinal))
            .Select(line => line[..line.IndexOf(':')])
            .ToArray();

        Assert.Equal(
            [
                "title", "source_kind", "source_path", "domain", "topic",
                "language", "file_type", "tags", "created_by", "source_service", "analysis_status",
            ],
            keys);
    }

    [Fact]
    public void Render_DelimitsBlockWithLeadingAndTrailingDashes()
    {
        var rendered = FrontmatterYaml.Render(CreateFrontmatter());
        var lines = rendered.Split('\n');

        Assert.Equal("---", lines[0]);
        Assert.Equal("---", lines[^2]); // lines[^1] is the trailing empty string after the final '\n'
    }

    [Fact]
    public void Render_NeverEmitsCarriageReturn_SoOutputIsByteIdenticalAcrossPlatforms()
    {
        var rendered = FrontmatterYaml.Render(CreateFrontmatter());

        Assert.DoesNotContain('\r', rendered);
    }

    // Title containing all four metacharacters WIKI-07 names: ':', '"', '#', and a leading '-'.
    [Fact]
    public void Render_TitleContainingYamlMetacharacters_RoundTripsIntact()
    {
        const string title = "-Order: \"Placed\" #Event";
        var rendered = FrontmatterYaml.Render(CreateFrontmatter(title));

        var content = string.Join(
            '\n', rendered.Split('\n').Where(line => line != "---" && line.Length > 0));
        var parsed = Deserializer.Deserialize<Dictionary<string, object>>(content);

        Assert.Equal(title, parsed["title"]);
        Assert.Null(FrontmatterYaml.Validate(rendered, "Acme.Orders/OrderService.cs"));
    }

    [Fact]
    public void Validate_WellFormedRenderedBlock_ReturnsNull()
    {
        var rendered = FrontmatterYaml.Render(CreateFrontmatter());

        var failure = FrontmatterYaml.Validate(rendered, "Acme.Orders/OrderService.cs");

        Assert.Null(failure);
    }

    [Fact]
    public void Validate_MalformedYaml_ReturnsFailureNamingFileAndErrorWithoutThrowing()
    {
        const string malformed = "---\ntitle: bad: value\n---\n";

        var failure = FrontmatterYaml.Validate(malformed, "Acme.Orders/Broken.cs");

        Assert.NotNull(failure);
        Assert.Equal("Acme.Orders/Broken.cs", failure!.SourcePath);
        Assert.False(string.IsNullOrEmpty(failure.Error));
    }

    [Fact]
    public void Validate_MissingRequiredField_ReturnsFailureNamingField()
    {
        const string missingDomain = """
            ---
            title: OrderService
            source_kind: codebase-file
            source_path: Acme.Orders/OrderService.cs
            topic: acme-shop
            language: csharp
            file_type: service
            tags: []
            created_by: csharp2md
            source_service: null
            analysis_status: pending
            ---
            """;

        var failure = FrontmatterYaml.Validate(missingDomain, "Acme.Orders/OrderService.cs");

        Assert.NotNull(failure);
        Assert.Contains("domain", failure!.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EmptyRequiredField_ReturnsFailureNamingField()
    {
        var rendered = FrontmatterYaml.Render(CreateFrontmatter(title: "OrderService"));
        var withEmptyTitle = rendered.Replace("title: OrderService", "title: \"\"", StringComparison.Ordinal);

        var failure = FrontmatterYaml.Validate(withEmptyTitle, "Acme.Orders/OrderService.cs");

        Assert.NotNull(failure);
        Assert.Contains("title", failure!.Error, StringComparison.Ordinal);
    }
}
