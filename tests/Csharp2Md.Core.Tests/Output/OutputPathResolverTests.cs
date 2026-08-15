using Csharp2Md.Core.Output;

namespace Csharp2Md.Core.Tests.Output;

public sealed class OutputPathResolverTests
{
    [Fact]
    public void DefaultForInput_NamedDirectory_ReturnsInputNamedSibling()
    {
        var parent = Directory.CreateTempSubdirectory("csharp2md-path-").FullName;
        try
        {
            var input = Path.Combine(parent, "src");

            var output = OutputPathResolver.DefaultForInput(input);

            Assert.Equal(Path.Combine(parent, "src_md"), output);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void DefaultForInput_FilesystemRoot_ReturnsNull()
    {
        var root = Path.GetPathRoot(Path.GetFullPath("."))!;

        var output = OutputPathResolver.DefaultForInput(root);

        Assert.Null(output);
    }
}
