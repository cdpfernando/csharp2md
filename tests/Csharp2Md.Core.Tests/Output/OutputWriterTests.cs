using Csharp2Md.Core.Output;
using Csharp2Md.Core.Rendering;
using Csharp2Md.Core.Tests.Rendering;

namespace Csharp2Md.Core.Tests.Output;

public sealed class OutputWriterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-output-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static RenderedDocument DocumentAt(string relativePath) =>
        RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace, relativePath);

    // P1-11: output path mirrors the document's relative path within its service source tree.
    [Fact]
    public void Write_MirrorsTheDocumentRelativePathBeneathTheOutputRoot()
    {
        var written = new OutputWriter(_root).Write(DocumentAt("Domain/Orders/OrderService.cs"));

        Assert.Equal(Path.Combine(_root, "Domain", "Orders", "OrderService.cs.md"), written);
        Assert.True(File.Exists(written));
    }

    [Fact]
    public void Write_DocumentAtTheServiceRoot_LandsDirectlyUnderTheOutputRoot()
    {
        var written = new OutputWriter(_root).Write(DocumentAt("Program.cs"));

        Assert.Equal(Path.Combine(_root, "Program.cs.md"), written);
    }

    [Fact]
    public void Write_PersistsTheRenderedMarkdown()
    {
        var document = DocumentAt("Orders/OrderService.cs");

        var written = new OutputWriter(_root).Write(document);

        Assert.Equal(document.ToMarkdown(), File.ReadAllText(written!));
    }

    [Theory]
    [InlineData("obj/Debug/net10.0/Generated.cs")]
    [InlineData("bin/Release/Thing.cs")]
    [InlineData("src/OBJ/Nested/Thing.cs")]
    [InlineData("src/Bin/Thing.cs")]
    public void Write_DocumentUnderABuildOutputDirectory_IsExcludedAndWritesNothing(string relativePath)
    {
        var written = new OutputWriter(_root).Write(DocumentAt(relativePath));

        Assert.Null(written);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Theory]
    [InlineData("Models/Contracts.g.cs")]
    [InlineData("Forms/MainForm.Designer.cs")]
    [InlineData("Forms/MainForm.designer.cs")]
    public void Write_GeneratedSourceFile_IsExcludedAndWritesNothing(string relativePath)
    {
        var written = new OutputWriter(_root).Write(DocumentAt(relativePath));

        Assert.Null(written);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Theory]
    [InlineData("Orders/OrderService.cs")]
    [InlineData("Objects/Registry.cs")]
    [InlineData("Binder/Setup.cs")]
    [InlineData("Orders/Designer.cs")]
    public void Write_OrdinaryHandWrittenSource_IsNotExcluded(string relativePath)
    {
        Assert.False(OutputWriter.IsExcluded(relativePath));
        Assert.NotNull(new OutputWriter(_root).Write(DocumentAt(relativePath)));
    }

    // P1-15: a rerun must not leave content behind for source that no longer exists.
    [Fact]
    public void PrepareRun_PrePopulatedOutputDirectory_RemovesEveryPriorFile()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Stale", "Deep"));
        File.WriteAllText(Path.Combine(_root, "Stale", "Deep", "Removed.cs.md"), "from a previous run");
        File.WriteAllText(Path.Combine(_root, "index.md"), "stale index");

        new OutputWriter(_root).PrepareRun();

        Assert.True(Directory.Exists(_root));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public void PrepareRun_MissingOutputDirectory_CreatesIt()
    {
        var missing = Path.Combine(_root, "not-yet-there");

        new OutputWriter(missing).PrepareRun();

        Assert.True(Directory.Exists(missing));
    }

    [Fact]
    public void PrepareRunThenWrite_OutputReflectsOnlyTheCurrentRun()
    {
        var writer = new OutputWriter(_root);
        File.WriteAllText(Path.Combine(_root, "Obsolete.cs.md"), "from a previous run");

        writer.PrepareRun();
        writer.Write(DocumentAt("Orders/OrderService.cs"));

        Assert.False(File.Exists(Path.Combine(_root, "Obsolete.cs.md")));
        Assert.Equal(
            [Path.Combine(_root, "Orders", "OrderService.cs.md")],
            Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }
}
