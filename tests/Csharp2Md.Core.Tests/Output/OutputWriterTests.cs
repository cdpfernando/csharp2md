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

    // P1-15 / CLI-12 / CLI-13: a marked rerun must not leave stale generated content behind.
    [Fact]
    public void PrepareRun_MarkedOutputDirectory_RemovesPriorContentAndRecreatesMarker()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;
        Directory.CreateDirectory(Path.Combine(output, "Stale", "Deep"));
        File.WriteAllText(Path.Combine(output, ".csharp2md-output"), "previous marker");
        File.WriteAllText(Path.Combine(output, "Stale", "Deep", "Removed.cs.md"), "from a previous run");
        File.WriteAllText(Path.Combine(output, "index.md"), "stale index");

        new OutputWriter(output).PrepareRun(input);

        Assert.Equal(
            [Path.Combine(output, ".csharp2md-output")],
            Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void PrepareRun_MissingOutputDirectory_CreatesItWithOwnershipMarker()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var missing = Path.Combine(_root, "not-yet-there");

        new OutputWriter(missing).PrepareRun(input);

        Assert.True(Directory.Exists(missing));
        Assert.True(File.Exists(Path.Combine(missing, ".csharp2md-output")));
    }

    [Fact]
    public void PrepareRun_EmptyOutputDirectory_CreatesOwnershipMarker()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;

        new OutputWriter(output).PrepareRun(input);

        Assert.Equal(
            [Path.Combine(output, ".csharp2md-output")],
            Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void PrepareRun_NonEmptyUnmarkedOutput_RefusesWithoutChangingContent()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;
        var existing = Path.Combine(output, "keep.txt");
        File.WriteAllText(existing, "keep me");

        var exception = Assert.Throws<OutputPreparationException>(
            () => new OutputWriter(output).PrepareRun(input));

        Assert.Contains("--force", exception.Message, StringComparison.Ordinal);
        Assert.Equal("keep me", File.ReadAllText(existing));
        Assert.Equal([existing], Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void PrepareRun_NonEmptyUnmarkedOutputWithForce_ReplacesContentAndCreatesMarker()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;
        var existing = Path.Combine(output, "remove.txt");
        File.WriteAllText(existing, "remove me");

        new OutputWriter(output).PrepareRun(input, force: true);

        Assert.False(File.Exists(existing));
        Assert.Equal(
            [Path.Combine(output, ".csharp2md-output")],
            Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void ValidateSafety_FilesystemRoot_IsRejectedBeforeFileOperations()
    {
        var root = Path.GetPathRoot(_root)!;

        var error = OutputWriter.ValidateSafety(root, _root);

        Assert.Contains("filesystem root", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrepareRun_OutputEqualsInput_RefusesEvenWithForceAndPreservesContent()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var existing = Path.Combine(input, "source.cs");
        File.WriteAllText(existing, "source");

        Assert.Throws<OutputPreparationException>(
            () => new OutputWriter(input).PrepareRun(input, force: true));

        Assert.Equal("source", File.ReadAllText(existing));
    }

    [Fact]
    public void PrepareRun_OutputIsInputAncestor_RefusesEvenWithForceAndPreservesContent()
    {
        var output = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
        var input = Directory.CreateDirectory(Path.Combine(output, "src")).FullName;
        var existing = Path.Combine(input, "source.cs");
        File.WriteAllText(existing, "source");

        Assert.Throws<OutputPreparationException>(
            () => new OutputWriter(output).PrepareRun(input, force: true));

        Assert.Equal("source", File.ReadAllText(existing));
    }

    [Fact]
    public void PrepareRun_ForceForEmptyOutput_DoesNotChangeSelectedPath()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var output = Path.Combine(_root, "selected-output");

        new OutputWriter(output).PrepareRun(input, force: true);

        Assert.True(File.Exists(Path.Combine(output, ".csharp2md-output")));
        Assert.False(Directory.Exists(Path.Combine(output, "input_md")));
    }

    [Fact]
    public void PrepareRunThenWrite_OutputReflectsOnlyTheCurrentRun()
    {
        var input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;
        var writer = new OutputWriter(output);
        File.WriteAllText(Path.Combine(output, ".csharp2md-output"), "marker");
        File.WriteAllText(Path.Combine(output, "Obsolete.cs.md"), "from a previous run");

        writer.PrepareRun(input);
        writer.Write(DocumentAt("Orders/OrderService.cs"));

        Assert.False(File.Exists(Path.Combine(output, "Obsolete.cs.md")));
        Assert.Equal(
            [Path.Combine(output, ".csharp2md-output"), Path.Combine(output, "Orders", "OrderService.cs.md")],
            Directory.GetFiles(output, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
    }
}
