using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Filesystem;

public sealed class FilesystemRefusalTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-21")]
    public void Open_FileAsOutputRoot_NamesThePathAndWritesNothing()
    {
        using var output = TempOutputRoot.Uncreated();
        File.WriteAllText(output.DirectoryPath, "not-a-directory");
        var before = File.ReadAllBytes(output.DirectoryPath);
        var lockPath = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey) + ".lock";

        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var exception = Assert.Throws<PublicationRejectedException>(() => store.Open(SolutionKey));

        Assert.Equal("not-a-package", exception.Gate);
        Assert.Contains(output.DirectoryPath, exception.Detail, StringComparison.Ordinal);
        Assert.True(File.Exists(output.DirectoryPath));
        Assert.False(Directory.Exists(output.DirectoryPath));
        Assert.False(File.Exists(lockPath));
        Assert.True(before.AsSpan().SequenceEqual(File.ReadAllBytes(output.DirectoryPath)));
    }

    [Fact]
    [Trait("Requirement", "STOR-20")]
    public void Commit_ChildWithoutManifest_LeavesTheDirectoryByteIdentical()
    {
        using var output = TempOutputRoot.Create();
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        Directory.CreateDirectory(child);
        var dummy = Path.Combine(child, "keep-me.txt");
        File.WriteAllText(dummy, "operator-owned");
        var before = FilesystemTestPaths.SnapshotFiles(child);
        var rootBefore = Directory.GetFileSystemEntries(output.DirectoryPath);

        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var exception = Assert.Throws<PublicationRejectedException>(() => store.Open(SolutionKey));

        Assert.Equal("not-a-package", exception.Gate);
        Assert.Contains(child, exception.Detail, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(child, "manifest.json")));
        Assert.Equal(rootBefore, Directory.GetFileSystemEntries(output.DirectoryPath));
        var after = FilesystemTestPaths.SnapshotFiles(child);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var key in before.Keys)
        {
            Assert.True(before[key].AsSpan().SequenceEqual(after[key]));
        }
    }
}
