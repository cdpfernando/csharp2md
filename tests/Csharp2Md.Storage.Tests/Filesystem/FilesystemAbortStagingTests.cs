using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class FilesystemAbortStagingTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-58")]
    public void Abort_WhileStagingDirectoryExists_DeletesStagingAndLeavesLastPackage()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var first = store.Open(SolutionKey);
        first.Stage(FactualSnapshot.Empty);
        first.Commit();
        var prior = FilesystemTestPaths.SnapshotFiles(child);

        var second = store.Open(SolutionKey);
        second.Stage(FactualSnapshot.Empty);
        var staging = child + ".staging";
        Directory.CreateDirectory(Path.Combine(staging, "facts"));
        File.WriteAllText(Path.Combine(staging, "facts", "structural.json"), "{}");
        Assert.True(Directory.Exists(staging));

        second.Abort();

        Assert.False(Directory.Exists(staging));
        Assert.False(File.Exists(child + ".lock"));
        var after = FilesystemTestPaths.SnapshotFiles(child);
        Assert.Equal(prior.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var key in prior.Keys)
        {
            Assert.True(prior[key].AsSpan().SequenceEqual(after[key]), $"Bytes at '{key}' changed.");
        }
    }
}
