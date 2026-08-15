using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Topic;

public sealed class FrontmatterTests
{
    // WIKI-06: FileType is the eleven-value classification the spec's file_type table names.
    [Fact]
    public void FileType_EnumeratesExactlyTheElevenSpecifiedValues()
    {
        var values = Enum.GetNames<FileType>();

        Assert.Equal(
            new[]
            {
                nameof(FileType.Class),
                nameof(FileType.Configuration),
                nameof(FileType.Controller),
                nameof(FileType.DataAccess),
                nameof(FileType.Enum),
                nameof(FileType.Extension),
                nameof(FileType.Filter),
                nameof(FileType.Handler),
                nameof(FileType.Index),
                nameof(FileType.Interface),
                nameof(FileType.Service),
            },
            values.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SourceKind_EnumeratesCodebaseFileAndCodebaseIndex()
    {
        var values = Enum.GetNames<SourceKind>();

        Assert.Equal(
            new[] { nameof(SourceKind.CodebaseFile), nameof(SourceKind.CodebaseIndex) },
            values.Order(StringComparer.Ordinal));
    }

    private static Frontmatter CreateFrontmatter(IReadOnlyList<string>? tags = null) => new(
        Title: "OrderService",
        SourceKind: SourceKind.CodebaseFile,
        SourcePath: "Acme.Orders/OrderService.cs",
        Domain: "system-design",
        Topic: "acme-shop",
        FileType: FileType.Service,
        Tags: tags ?? []);

    [Fact]
    public void Language_IsAlwaysCsharp()
    {
        Assert.Equal("csharp", CreateFrontmatter().Language);
    }

    [Fact]
    public void CreatedBy_IsAlwaysCsharp2Md()
    {
        Assert.Equal("csharp2md", CreateFrontmatter().CreatedBy);
    }

    [Fact]
    public void SourceService_IsAlwaysNullInPhase1()
    {
        Assert.Null(CreateFrontmatter().SourceService);
    }

    [Fact]
    public void AnalysisStatus_IsAlwaysPendingInPhase1()
    {
        Assert.Equal("pending", CreateFrontmatter().AnalysisStatus);
    }

    [Fact]
    public void Tags_ConstructedEmpty_IsNeverNull()
    {
        var frontmatter = CreateFrontmatter([]);

        Assert.NotNull(frontmatter.Tags);
        Assert.Empty(frontmatter.Tags);
    }
}
