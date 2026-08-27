using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class ContributionRegistrationTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void Commit_StagingBlockedOnFirstPublish_LeavesNoContributionInTheAccumulator()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new RecordingComposer());
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        File.WriteAllText(child + ".staging", "blocked");

        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        Assert.Throws<PublicationRejectedException>(session.Commit);
        session.Abort();

        Assert.Empty(store.AccumulatedContributions);
        Assert.False(Directory.Exists(child));
    }

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void Commit_StagingBlockedOnReplace_DoesNotRegisterTheFailedContribution()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new RecordingComposer());
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var identity = SolutionCoordinate.For(SolutionKey).Identity.Value;

        var first = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        first.Stage(FactualSnapshot.Empty);
        first.Commit();
        var retained = store.AccumulatedContributions[identity];

        File.WriteAllText(child + ".staging", "blocked");
        var second = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        second.Stage(FactualSnapshot.Empty);
        Assert.Throws<PublicationRejectedException>(second.Commit);
        second.Abort();

        Assert.Same(retained, store.AccumulatedContributions[identity]);
    }

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void Commit_ReplaceWhilePublishedFileIsHeld_DoesNotRegisterTheFailedContribution()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(
            output.DirectoryPath,
            projector: null,
            composer: new RecordingComposer(),
            FilesystemRetryPolicy.FastFail);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var identity = SolutionCoordinate.For(SolutionKey).Identity.Value;

        var first = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        first.Stage(FactualSnapshot.Empty);
        first.Commit();
        var retained = store.AccumulatedContributions[identity];

        var second = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        second.Stage(FactualSnapshot.Empty);
        var manifest = Path.Combine(child, "manifest.json");
        using (var held = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            if (!OperatingSystem.IsWindows())
            {
                second.Commit();
                Assert.NotSame(retained, store.AccumulatedContributions[identity]);
                return;
            }

            Assert.Throws<PublicationRejectedException>(second.Commit);
        }

        second.Abort();
        Assert.Same(retained, store.AccumulatedContributions[identity]);
    }

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void Abort_AfterOpen_LeavesNoContributionInTheAccumulator()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new RecordingComposer());
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Abort();

        Assert.Empty(store.AccumulatedContributions);
        Assert.False(Directory.Exists(FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey)));
    }

    private sealed class RecordingComposer : IBatchComposer
    {
        public SolutionContribution Contribute(
            PublishedPackageView view,
            SolutionCoordinate coordinate,
            string packageDirectory) =>
            new(
                coordinate.Identity.Value,
                coordinate.SolutionFileName,
                packageDirectory,
                [],
                [],
                [],
                [],
                []);

        public ImmutableArray<StagedFragment> Compose(BatchView batch) => [];
    }
}
