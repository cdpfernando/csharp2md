using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Filesystem;

public sealed class FilesystemLockTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-59")]
    public void Open_OverlappingSameChild_IsRejectedUntilCommitOrAbort()
    {
        using var output = TempOutputRoot.Create();
        var storeA = new FilesystemTransactionalStore(output.DirectoryPath);
        var storeB = new FilesystemTransactionalStore(output.DirectoryPath);
        var first = storeA.Open(SolutionKey);

        var locked = Assert.Throws<PublicationRejectedException>(() => storeB.Open(SolutionKey));
        Assert.Equal("lock", locked.Gate);
        Assert.Contains(output.DirectoryPath, locked.Detail, StringComparison.Ordinal);

        first.Abort();
        var afterAbort = storeB.Open(SolutionKey);
        afterAbort.Stage(FactualSnapshot.Empty);
        afterAbort.Commit();

        var afterCommit = storeA.Open(SolutionKey);
        afterCommit.Abort();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        Assert.True(File.Exists(Path.Combine(child, "manifest.json")));
        Assert.False(File.Exists(child + ".lock"));
    }

    [Fact]
    [Trait("Requirement", "STOR-59")]
    public void Open_FailedOverlappingOpen_DoesNotHoldTheLock()
    {
        using var output = TempOutputRoot.Create();
        var storeA = new FilesystemTransactionalStore(output.DirectoryPath);
        var storeB = new FilesystemTransactionalStore(output.DirectoryPath);
        var first = storeA.Open(SolutionKey);

        var locked = Assert.Throws<PublicationRejectedException>(() => storeB.Open(SolutionKey));
        Assert.Equal("lock", locked.Gate);

        first.Abort();
        var recovered = storeB.Open(SolutionKey);
        recovered.Abort();
        Assert.False(File.Exists(FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey) + ".lock"));
    }
}
