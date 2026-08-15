using Csharp2Md.Core;
using Csharp2Md.Core.Output;
using Csharp2Md.Core.Topic;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Output;

public sealed class IndexWriterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-index-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static TopicOptions Options() => TopicOptions.Create("acme-shop", "system-design", "input-root").Options!;

    private string ServiceRoot(string service) => Path.Combine(_root, service);

    private string[] OrdersFiles() =>
    [
        Path.Combine(ServiceRoot("Acme.Orders"), "OrderService.cs.md"),
        Path.Combine(ServiceRoot("Acme.Orders"), "Domain", "Money.cs.md"),
        Path.Combine(ServiceRoot("Acme.Orders"), "Domain", "Orders", "Order.cs.md"),
    ];

    private string WriteOrdersIndex() =>
        IndexWriter.WriteServiceIndex(ServiceRoot("Acme.Orders"), new ServiceName("Acme.Orders"), OrdersFiles(), Options());

    // P1-13: one index.md per service, at the root of that service's output folder.
    [Fact]
    public void WriteServiceIndex_LandsAtTheServiceOutputRoot()
    {
        var path = WriteOrdersIndex();

        Assert.Equal(Path.Combine(ServiceRoot("Acme.Orders"), "index.md"), path);
        Assert.True(File.Exists(path));
    }

    // P1-13: it links *every* generated file for that service — none omitted.
    [Fact]
    public void WriteServiceIndex_LinksEveryGeneratedFileForTheService()
    {
        var content = File.ReadAllText(WriteOrdersIndex());

        Assert.Equal(
            new[]
            {
                "- [Domain/Money.cs](./Domain/Money.cs.md)",
                "- [Domain/Orders/Order.cs](./Domain/Orders/Order.cs.md)",
                "- [OrderService.cs](./OrderService.cs.md)",
            },
            content.Split('\n').Where(line => line.StartsWith("- ", StringComparison.Ordinal)));
    }

    [Fact]
    public void WriteServiceIndex_HeadsTheFileWithTheServiceName()
    {
        // WIKI-05: the heading itself is unchanged; it now follows the frontmatter block rather than
        // starting the file.
        Assert.Contains("\n---\n\n# Acme.Orders\n", File.ReadAllText(WriteOrdersIndex()), StringComparison.Ordinal);
    }

    // WIKI-05/WIKI-06: both index writers emit the same block shape a source document's frontmatter
    // does, and it must round-trip through the same validator.
    [Fact]
    public void WriteServiceIndex_FrontmatterBlock_ValidatesUnderTheSameValidatorDocumentBlocksUse()
    {
        var block = ExtractFrontmatterBlock(File.ReadAllText(WriteOrdersIndex()));

        Assert.Null(FrontmatterYaml.Validate(block, "Acme.Orders/index.md"));
    }

    [Fact]
    public void WriteRootIndex_FrontmatterBlock_ValidatesUnderTheSameValidatorDocumentBlocksUse()
    {
        var path = IndexWriter.WriteRootIndex(
            _root,
            [new ServiceIndexEntry(new ServiceName("Acme.Orders"), Path.Combine(ServiceRoot("Acme.Orders"), "index.md"))],
            Options());

        var block = ExtractFrontmatterBlock(File.ReadAllText(path));

        Assert.Null(FrontmatterYaml.Validate(block, "index.md"));
    }

    private static string ExtractFrontmatterBlock(string content)
    {
        Assert.StartsWith("---\n", content, StringComparison.Ordinal);
        var closingDelimiterIndex = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        Assert.True(closingDelimiterIndex > 0, "expected a closing '---' delimiter line");

        return content[..(closingDelimiterIndex + "\n---\n".Length)];
    }

    // Spec Assumptions: links stay relative and use forward slashes, so the tree is portable to
    // GitHub and to an IDE without a server.
    [Fact]
    public void WriteServiceIndex_LinksAreRelativeAndUseForwardSlashes()
    {
        var links = Links(File.ReadAllText(WriteOrdersIndex()));

        Assert.All(links, link => Assert.StartsWith("./", link, StringComparison.Ordinal));
        Assert.All(links, link => Assert.DoesNotContain('\\', link));
        Assert.All(links, link => Assert.DoesNotContain(_root, link, StringComparison.Ordinal));
    }

    // P1-14: one root index.md linking every per-service index.
    [Fact]
    public void WriteRootIndex_LinksEveryPerServiceIndex()
    {
        var path = IndexWriter.WriteRootIndex(
            _root,
            [
                new ServiceIndexEntry(new ServiceName("Acme.Payments"), Path.Combine(ServiceRoot("Acme.Payments"), "index.md")),
                new ServiceIndexEntry(new ServiceName("Acme.Orders"), Path.Combine(ServiceRoot("Acme.Orders"), "index.md")),
            ],
            Options());

        Assert.Equal(Path.Combine(_root, "index.md"), path);
        Assert.Equal(
            new[]
            {
                "- [Acme.Orders](./Acme.Orders/index.md)",
                "- [Acme.Payments](./Acme.Payments/index.md)",
            },
            File.ReadAllText(path).Split('\n').Where(line => line.StartsWith("- ", StringComparison.Ordinal)));
    }

    [Fact]
    public void WriteRootIndex_LinksAreRelativeAndUseForwardSlashes()
    {
        var path = IndexWriter.WriteRootIndex(
            _root,
            [new ServiceIndexEntry(new ServiceName("Acme.Orders"), Path.Combine(ServiceRoot("Acme.Orders"), "index.md"))],
            Options());

        var link = Assert.Single(Links(File.ReadAllText(path)));

        Assert.Equal("./Acme.Orders/index.md", link);
    }

    // A service whose documents were all excluded still gets an index, so the root index never
    // links at a file that does not exist.
    [Fact]
    public void WriteServiceIndex_ServiceWithNoGeneratedFiles_StillWritesAnIndexWithNoLinks()
    {
        var path = IndexWriter.WriteServiceIndex(ServiceRoot("Acme.Empty"), new ServiceName("Acme.Empty"), [], Options());

        Assert.True(File.Exists(path));
        Assert.Empty(Links(File.ReadAllText(path)));
    }

    [Fact]
    public Task WriteServiceIndex_Shape_MatchesApprovedSnapshot() =>
        Verifier.Verify(File.ReadAllText(WriteOrdersIndex()), "md").UseDirectory("snapshots");

    [Fact]
    public Task WriteRootIndex_Shape_MatchesApprovedSnapshot()
    {
        var path = IndexWriter.WriteRootIndex(
            _root,
            [
                new ServiceIndexEntry(new ServiceName("Acme.Orders"), Path.Combine(ServiceRoot("Acme.Orders"), "index.md")),
                new ServiceIndexEntry(new ServiceName("Acme.Payments"), Path.Combine(ServiceRoot("Acme.Payments"), "index.md")),
            ],
            Options());

        return Verifier.Verify(File.ReadAllText(path), "md").UseDirectory("snapshots");
    }

    private static List<string> Links(string markdown) =>
        markdown.Split('\n')
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf('(', StringComparison.Ordinal) + 1)..line.LastIndexOf(')')])
            .ToList();
}
