using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Filesystem;

public sealed class FilesystemIoFailureTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-23")]
    public void Commit_StagingPathOccupiedByAFile_NamesIoAndPreservesPriorPackage()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var first = store.Open(SolutionKey);
        first.Stage(FactualSnapshot.Empty);
        first.Commit();
        var prior = FilesystemTestPaths.SnapshotFiles(child);
        Assert.Contains("manifest.json", prior.Keys);

        var stagingPath = child + ".staging";
        File.WriteAllText(stagingPath, "blocked");

        var second = store.Open(SolutionKey);
        second.Stage(FactualSnapshot.Empty);
        var exception = Assert.Throws<PublicationRejectedException>(second.Commit);

        Assert.Equal("io", exception.Gate);
        Assert.False(string.IsNullOrWhiteSpace(exception.Detail));
        Assert.Contains(stagingPath, exception.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(stagingPath));
        AssertEqualSnapshots(prior, FilesystemTestPaths.SnapshotFiles(child));
        second.Abort();
    }

    private static void AssertEqualSnapshots(
        IReadOnlyDictionary<string, byte[]> left,
        IReadOnlyDictionary<string, byte[]> right)
    {
        Assert.Equal(left.Keys.Order(StringComparer.Ordinal), right.Keys.Order(StringComparer.Ordinal));
        foreach (var key in left.Keys)
        {
            Assert.True(left[key].AsSpan().SequenceEqual(right[key]), $"Bytes at '{key}' changed.");
        }
    }
}
