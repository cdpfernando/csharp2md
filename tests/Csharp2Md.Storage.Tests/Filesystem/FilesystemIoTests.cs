namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class FilesystemIoTests
{
    [Fact]
    [Trait("Requirement", "STOR-23")]
    public void MoveDirectory_DestinationExists_NamesBothPaths()
    {
        var root = Directory.CreateTempSubdirectory("csharp2md-io-move-");
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root.FullName, "src"));
            File.WriteAllText(Path.Combine(source.FullName, "a.txt"), "a");
            var destination = Directory.CreateDirectory(Path.Combine(root.FullName, "dst"));
            File.WriteAllText(Path.Combine(destination.FullName, "b.txt"), "b");

            var exception = Assert.Throws<IOException>(
                () => FilesystemIo.MoveDirectory(
                    source.FullName,
                    destination.FullName,
                    FilesystemRetryPolicy.FastFail));

            Assert.Contains(source.FullName, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(destination.FullName, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(source.FullName, "a.txt")));
            Assert.True(File.Exists(Path.Combine(destination.FullName, "b.txt")));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-23")]
    public void DeleteDirectory_MissingPath_DoesNotThrow()
    {
        var missing = Path.Combine(Path.GetTempPath(), "csharp2md-io-missing-" + Guid.NewGuid().ToString("N"));

        FilesystemIo.DeleteDirectory(missing, FilesystemRetryPolicy.FastFail);

        Assert.False(Directory.Exists(missing));
    }
}
