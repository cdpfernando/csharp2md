using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class FilesystemAtomicReplaceTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-17")]
    public void Commit_SecondSuccessfulRun_ReplacesTheChildWithNoMixedShards()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var first = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        first.Stage(CandidateSnapshot());
        first.Commit();
        Assert.True(File.Exists(Path.Combine(child, "relations", "candidates.json")));
        Assert.False(File.Exists(Path.Combine(child, "facts", "structural.json")));

        var second = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        second.Stage(SolutionSnapshot());
        second.Commit();

        var onDisk = FilesystemTestPaths.SnapshotFiles(child);
        Assert.Contains("facts/structural.json", onDisk.Keys);
        Assert.DoesNotContain("relations/candidates.json", onDisk.Keys);
        Assert.False(Directory.Exists(child + ".staging"));
        Assert.False(Directory.Exists(child + ".bak"));
        Assert.True(File.Exists(Path.Combine(child, "manifest.json")));
    }

    [Fact]
    [Trait("Requirement", "STOR-18")]
    [Trait("Requirement", "STOR-31")]
    public void Commit_ValidationFailureAfterSuccessfulPublish_LeavesPriorFilesUnchanged()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var first = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        first.Stage(CandidateSnapshot());
        first.Commit();
        var prior = FilesystemTestPaths.SnapshotFiles(child);
        Assert.Contains("relations/candidates.json", prior.Keys);
        Assert.Contains("manifest.json", prior.Keys);

        var colliding = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        colliding.Stage(SolutionSnapshot());
        colliding.Stage(SolutionSnapshot());
        var exception = Assert.Throws<PublicationRejectedException>(colliding.Commit);

        Assert.Equal("identity-collision", exception.Gate);
        Assert.False(Directory.Exists(child + ".staging"));
        var after = FilesystemTestPaths.SnapshotFiles(child);
        AssertEqualSnapshots(prior, after);
    }

    [Fact]
    [Trait("Requirement", "STOR-18")]
    public void Abort_SecondSession_LeavesPriorPackageByteIdentical()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var first = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        first.Stage(CandidateSnapshot());
        first.Commit();
        var prior = FilesystemTestPaths.SnapshotFiles(child);

        var second = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        second.Stage(SolutionSnapshot());
        second.Abort();

        Assert.False(Directory.Exists(child + ".staging"));
        AssertEqualSnapshots(prior, FilesystemTestPaths.SnapshotFiles(child));
        Assert.True(File.Exists(Path.Combine(child, "relations", "candidates.json")));
        Assert.False(File.Exists(Path.Combine(child, "facts", "structural.json")));
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

    private static FactualSnapshot SolutionSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));
        return new FactualSnapshot([solution], [], [], [], [], []);
    }

    private static FactualSnapshot CandidateSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var link = CandidateLink.Create(
            RelationKind.Contains,
            Solution.Create(solutionId).Reference,
            Project.Create(projectId).Reference,
            EvidenceChain.Create([
                new ObservationIdentity(
                    Solution.Create(solutionId).Reference,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    1)]));
        return new FactualSnapshot([], [], [], [link], [], []);
    }
}
