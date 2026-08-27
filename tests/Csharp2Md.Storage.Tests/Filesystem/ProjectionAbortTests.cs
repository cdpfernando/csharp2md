using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class ProjectionAbortTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";
    private const string FragmentKey = "projections/sample.json";

    private static readonly ImmutableArray<byte> FragmentBytes =
        Encoding.UTF8.GetBytes("{\"ok\":true}").ToImmutableArray();

    [Fact]
    [Trait("Requirement", "RP-04")]
    public void Commit_ThrowingProjector_LeavesPriorFileBytesUnchanged()
    {
        using var output = TempOutputRoot.Create();
        var (prior, after, _, _) = CommitWorkingThenThrowing(output);

        Assert.Equal(prior.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var key in prior.Keys)
        {
            Assert.True(prior[key].AsSpan().SequenceEqual(after[key]), $"Bytes at '{key}' changed.");
        }
    }

    [Fact]
    [Trait("Requirement", "RP-04")]
    public void Commit_ThrowingProjector_LeavesNoStagingDirectory()
    {
        using var output = TempOutputRoot.Create();
        var (_, _, child, _) = CommitWorkingThenThrowing(output);

        Assert.False(Directory.Exists(child + ".staging"));
    }

    [Fact]
    [Trait("Requirement", "RP-04")]
    public void Commit_ThrowingProjector_LeavesNoBakDirectory()
    {
        using var output = TempOutputRoot.Create();
        var (_, _, child, _) = CommitWorkingThenThrowing(output);

        Assert.False(Directory.Exists(child + ".bak"));
    }

    [Fact]
    [Trait("Requirement", "RP-04")]
    public void Commit_ThrowingProjector_RejectsWithProjectionReason()
    {
        using var output = TempOutputRoot.Create();
        var (_, _, _, exception) = CommitWorkingThenThrowing(output);

        Assert.Equal("projection", exception.Gate);
        Assert.Contains("projection", exception.Message, StringComparison.Ordinal);
    }

    private static (
        IReadOnlyDictionary<string, byte[]> Prior,
        IReadOnlyDictionary<string, byte[]> After,
        string Child,
        PublicationRejectedException Exception)
        CommitWorkingThenThrowing(TempOutputRoot output)
    {
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var working = new FilesystemTransactionalStore(output.DirectoryPath, new WorkingProjector());
        var first = working.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        first.Stage(FactualSnapshot.Empty);
        first.Commit();
        var prior = FilesystemTestPaths.SnapshotFiles(child);
        Assert.Contains(FragmentKey, prior.Keys);

        var throwing = new FilesystemTransactionalStore(output.DirectoryPath, new ThrowingProjector());
        var second = throwing.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        second.Stage(FactualSnapshot.Empty);
        var exception = Assert.Throws<PublicationRejectedException>(second.Commit);
        var after = FilesystemTestPaths.SnapshotFiles(child);
        return (prior, after, child, exception);
    }

    private sealed class WorkingProjector : IPackageProjector
    {
        public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(source);
            return [new StagedFragment(ArtifactRole.Payload, FragmentKey, FragmentBytes)];
        }
    }

    private sealed class ThrowingProjector : IPackageProjector
    {
        public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source) =>
            throw new InvalidOperationException("projector failed");
    }
}
