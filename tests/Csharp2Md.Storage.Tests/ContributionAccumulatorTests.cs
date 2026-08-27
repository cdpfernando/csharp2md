using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests;

public sealed class ContributionAccumulatorTests
{
    private const string FirstSolution = @"C:\src\Acme.Orders.slnx";
    private const string SecondSolution = @"C:\src\Acme.Payments.slnx";

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void Abort_BeforeCommit_LeavesNoContributionInTheAccumulator()
    {
        var composer = new RecordingComposer();
        var store = new InMemoryTransactionalStore(composer: composer);
        var session = store.Open(FirstSolution, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Abort();

        Assert.Empty(store.AccumulatedContributions);
        Assert.Equal(0, composer.ContributeCalls);
    }

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void TwoSequentialBatches_OnOneStoreInstance_DoNotShareContributions()
    {
        var store = new InMemoryTransactionalStore(composer: new RecordingComposer());
        Commit(store, FirstSolution);
        var firstIdentity = SolutionCoordinate.For(FirstSolution).Identity.Value;
        Assert.True(store.AccumulatedContributions.ContainsKey(firstIdentity));

        store.PublishBatch(Record(FirstSolution, PublicationStatus.Committed));
        Assert.Empty(store.AccumulatedContributions);

        Commit(store, SecondSolution);
        var secondIdentity = SolutionCoordinate.For(SecondSolution).Identity.Value;
        Assert.False(store.AccumulatedContributions.ContainsKey(firstIdentity));
        Assert.True(store.AccumulatedContributions.ContainsKey(secondIdentity));
        Assert.Equal(secondIdentity, Assert.Single(store.AccumulatedContributions).Key);
    }

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void Publish_BuildsTheViewOnceWhenProjectorAndComposerBothRun()
    {
        var projector = new CountingProjector();
        var composer = new RecordingComposer();
        var builds = 0;
        var coordinate = SolutionCoordinate.For(FirstSolution);

        var outcome = PublicationPipeline.Publish(
            FactualSnapshot.Empty,
            new ManifestContext(coordinate.Identity.Value, coordinate.SolutionFileName),
            coordinate,
            "s-test",
            projector,
            composer,
            EmptySourceDocumentReader.Instance,
            document =>
            {
                builds++;
                return PublishedPackageView.From(document);
            });

        Assert.Equal(1, builds);
        Assert.Equal(1, projector.ProjectCalls);
        Assert.Equal(1, composer.ContributeCalls);
        Assert.Same(projector.View, composer.View);
        Assert.NotNull(outcome.Contribution);
        Assert.Equal(coordinate.Identity.Value, outcome.Contribution.SolutionIdentity);
    }

    private static void Commit(InMemoryTransactionalStore store, string solutionPath)
    {
        var session = store.Open(solutionPath, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();
    }

    private static ImmutableArray<BatchSolutionRecord> Record(string solutionPath, PublicationStatus status)
    {
        var coordinate = SolutionCoordinate.For(solutionPath);
        return [new BatchSolutionRecord(coordinate.Identity, coordinate.SolutionFileName, status, null)];
    }

    private sealed class RecordingComposer : IBatchComposer
    {
        public int ContributeCalls { get; private set; }

        public PublishedPackageView? View { get; private set; }

        public SolutionContribution Contribute(
            PublishedPackageView view,
            SolutionCoordinate coordinate,
            string packageDirectory)
        {
            ContributeCalls++;
            View = view;
            return new SolutionContribution(
                coordinate.Identity.Value,
                coordinate.SolutionFileName,
                packageDirectory,
                [],
                [],
                [],
                [],
                []);
        }

        public ImmutableArray<StagedFragment> Compose(BatchView batch) => [];
    }

    private sealed class CountingProjector : IPackageProjector
    {
        public int ProjectCalls { get; private set; }

        public PublishedPackageView? View { get; private set; }

        public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
        {
            ProjectCalls++;
            View = view;
            return [];
        }
    }
}
