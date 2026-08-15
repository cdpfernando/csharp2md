using Csharp2Md.Core.Rendering;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Rendering;

/// <summary>
/// WIKI-05/WIKI-08: the frontmatter block is a decorator over <c>ToMarkdown()</c>, mirroring how
/// <see cref="RenderedDocument.DependencySection"/> is attached — it maps to no source bytes, so it
/// lives outside <c>Sections</c> and the three <c>SpanCoverageTests</c> (which assert only over
/// <c>Sections</c>) need no change at all.
/// </summary>
public sealed class RenderedDocumentFrontmatterTests
{
    private static Frontmatter SampleFrontmatter() => new(
        Title: "OrderService",
        SourceKind: SourceKind.CodebaseFile,
        SourcePath: "Acme.Orders/OrderService.cs",
        Domain: "system-design",
        Topic: "acme-shop",
        FileType: FileType.Service,
        Tags: ["async-patterns", "event-driven"]);

    [Fact]
    public void ToMarkdown_WithFrontmatter_PrependsDelimitedBlockBeforeTheHeading()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace) with
        {
            Frontmatter = SampleFrontmatter(),
        };

        var markdown = document.ToMarkdown();

        Assert.StartsWith("---\n", markdown, StringComparison.Ordinal);
        var closingDelimiterIndex = markdown.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        Assert.True(closingDelimiterIndex > 0, "expected a closing '---' delimiter line");

        var headingIndex = markdown.IndexOf("# ", StringComparison.Ordinal);
        Assert.True(
            headingIndex > closingDelimiterIndex,
            "expected the '# ' heading to follow the closing frontmatter delimiter");
    }

    // WIKI-08: stripping the block must reproduce exactly what ToMarkdown() emits without
    // frontmatter — not merely "something similar". FrontmatterYaml.Render's own trailing newline
    // plus the one blank-line separator ToMarkdown adds is the whole prefix; everything after it is
    // required to be the undecorated render, byte for byte.
    [Fact]
    public void ToMarkdown_WithFrontmatter_BodyBelowTheBlockIsByteIdenticalToTheUndecoratedRender()
    {
        var undecorated = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace);
        var frontmatter = SampleFrontmatter();
        var decorated = undecorated with { Frontmatter = frontmatter };

        var decoratedMarkdown = decorated.ToMarkdown();
        var undecoratedMarkdown = undecorated.ToMarkdown();
        var expectedPrefix = FrontmatterYaml.Render(frontmatter) + "\n";

        Assert.StartsWith(expectedPrefix, decoratedMarkdown, StringComparison.Ordinal);
        Assert.Equal(undecoratedMarkdown, decoratedMarkdown[expectedPrefix.Length..]);
    }

    [Fact]
    public void ToMarkdown_WithNoFrontmatter_RendersExactlyAsBefore()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace);

        var markdown = document.ToMarkdown();

        Assert.StartsWith("# " + document.RelativePath, markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("---\n", markdown, StringComparison.Ordinal);
    }
}
