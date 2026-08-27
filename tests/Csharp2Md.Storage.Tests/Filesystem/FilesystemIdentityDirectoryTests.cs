using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class FilesystemIdentityDirectoryTests
{
    [Fact]
    [Trait("Requirement", "MSC-02")]
    [Trait("Requirement", "MSC-05")]
    public void Open_SameFileNameUnderTwoParents_ProducesOneDirectoryName()
    {
        using var output = TempOutputRoot.Create();
        var left = Path.Combine(output.DirectoryPath, "clone-a", "Acme.Orders.slnx");
        var right = Path.Combine(output.DirectoryPath, "clone-b", "Acme.Orders.slnx");
        var store = new FilesystemTransactionalStore(output.DirectoryPath);

        var first = store.Open(left, EmptySourceDocumentReader.Instance);
        first.Stage(FactualSnapshot.Empty);
        first.Commit();
        var nameAfterLeft = Path.GetFileName(Assert.Single(Directory.GetDirectories(output.DirectoryPath)));

        var second = store.Open(right, EmptySourceDocumentReader.Instance);
        second.Stage(FactualSnapshot.Empty);
        second.Commit();
        var nameAfterRight = Path.GetFileName(Assert.Single(Directory.GetDirectories(output.DirectoryPath)));

        Assert.Equal(nameAfterLeft, nameAfterRight);
        Assert.Equal(FilesystemTestPaths.ChildName(left), nameAfterLeft);
        Assert.Equal(FilesystemTestPaths.ChildName(right), nameAfterRight);
        Assert.NotEqual(
            "s-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(left)))[..32],
            nameAfterLeft);
        Assert.NotEqual(
            "s-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(right)))[..32],
            nameAfterRight);
    }

    [Fact]
    [Trait("Requirement", "MSC-02")]
    public void Open_LockAndStagingSiblings_FollowTheIdentityDirectoryStem()
    {
        using var output = TempOutputRoot.Create();
        var path = Path.Combine(output.DirectoryPath, "Acme.Orders.slnx");
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var stem = FilesystemTestPaths.ChildName(path);
        var session = store.Open(path, EmptySourceDocumentReader.Instance);
        try
        {
            var lockPath = Path.Combine(output.DirectoryPath, stem + ".lock");
            var stagingPath = Path.Combine(output.DirectoryPath, stem + ".staging");
            Assert.True(File.Exists(lockPath));
            Assert.Equal(stem + ".lock", Path.GetFileName(Assert.Single(Directory.GetFiles(output.DirectoryPath, "*.lock"))));

            Directory.CreateDirectory(stagingPath);
            File.WriteAllText(Path.Combine(stagingPath, "probe.txt"), "staging");
            Assert.True(Directory.Exists(stagingPath));

            session.Abort();

            Assert.False(File.Exists(lockPath));
            Assert.False(Directory.Exists(stagingPath));
        }
        catch
        {
            session.Abort();
            throw;
        }
    }
}
