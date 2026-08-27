using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class BatchPublicationEngineTests
{
    [Fact]
    [Trait("Requirement", "MSC-03")]
    public async Task AnalyzeAsync_EveryRequestedSolution_YieldsABatchRecord()
    {
        var store = new RecordingBatchStore(new InMemoryTransactionalStore());
        var stages = StubStages.CreateDefault()
            .SetItem(3, new RecordingStage("Classification and Promotion", [], context =>
            {
                if (context.SolutionPath == "payments.slnx")
                {
                    throw new InvalidOperationException("forced unpublished");
                }
            }));
        var engine = new AnalysisEngine(store, stages);

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create(["orders.slnx", "payments.slnx"]),
            CancellationToken.None);

        Assert.Equal(2, result.Solutions.Length);
        Assert.Equal(2, store.Published.Length);
        Assert.Contains(
            store.Published,
            record => record.Status == PublicationStatus.Committed
                && record.SolutionFileName == "orders.slnx"
                && record.FailingStage is null);
        Assert.Contains(
            store.Published,
            record => record.Status == PublicationStatus.Unpublished
                && record.SolutionFileName == "payments.slnx"
                && record.FailingStage == "Classification and Promotion");
        Assert.All(
            store.Published,
            record => Assert.Contains(
                result.Solutions,
                outcome => outcome.Status == record.Status
                    && Path.GetFileName(outcome.SolutionPath) == record.SolutionFileName));
    }

    [Fact]
    [Trait("Requirement", "MSC-09")]
    public async Task AnalyzeAsync_UnpublishedSolution_CarriesFailingStageAndLeavesCommittedPackageUnmodified()
    {
        var output = Directory.CreateTempSubdirectory("csharp2md-batch-engine-");
        try
        {
            var inner = new FilesystemTransactionalStore(output.FullName);
            var store = new SnapshottingBatchStore(inner, output.FullName);
            var stages = StubStages.CreateDefault()
                .SetItem(3, new RecordingStage("Classification and Promotion", [], context =>
                {
                    if (context.SolutionPath == "payments.slnx")
                    {
                        throw new InvalidOperationException("forced unpublished");
                    }
                }));
            var engine = new AnalysisEngine(store, stages);

            var result = await engine.AnalyzeAsync(
                AnalysisRequest.Create(["orders.slnx", "payments.slnx"]),
                CancellationToken.None);

            var unpublished = Assert.Single(result.Solutions, outcome => outcome.Status == PublicationStatus.Unpublished);
            Assert.Equal("Classification and Promotion", unpublished.FailingStage);
            Assert.NotNull(store.PackagesBeforeBatch);
            AssertEqualSnapshots(store.PackagesBeforeBatch, SnapshotPackages(output.FullName));
            var unpublishedRecord = Assert.Single(
                store.Published,
                record => record.Status == PublicationStatus.Unpublished);
            Assert.Equal("Classification and Promotion", unpublishedRecord.FailingStage);
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "MSC-35")]
    [Trait("Requirement", "MSC-03")]
    public async Task AnalyzeAsync_EverySolutionUnpublished_PublishesManifestAndNoComposition()
    {
        var output = Directory.CreateTempSubdirectory("csharp2md-batch-unpublished-");
        try
        {
            var store = new FilesystemTransactionalStore(output.FullName);
            var stages = StubStages.CreateDefault().SetItem(0, new ThrowingStage("Inventory"));
            var engine = new AnalysisEngine(store, stages);

            var result = await engine.AnalyzeAsync(
                AnalysisRequest.Create(["orders.slnx", "payments.slnx"]),
                CancellationToken.None);

            Assert.Equal(2, result.Solutions.Length);
            Assert.All(result.Solutions, outcome => Assert.Equal(PublicationStatus.Unpublished, outcome.Status));
            Assert.All(result.Solutions, outcome => Assert.Equal("Inventory", outcome.FailingStage));

            var manifestPath = Path.Combine(output.FullName, "batch-manifest.json");
            Assert.True(File.Exists(manifestPath));
            var envelope = CanonicalJson.Read<BatchManifestEnvelope>(File.ReadAllBytes(manifestPath));
            Assert.False(envelope.Complete);
            Assert.Equal("solution-unpublished", envelope.IncompleteScopeReason);
            Assert.Equal(2, envelope.Solutions.Length);
            Assert.All(envelope.Solutions, entry => Assert.Equal("unpublished", entry.Status));
            Assert.All(envelope.Solutions, entry => Assert.Equal("Inventory", entry.FailingStage));
            Assert.True(envelope.Artifacts.IsDefaultOrEmpty || envelope.Artifacts.Length == 0);
            Assert.False(Directory.Exists(Path.Combine(output.FullName, "composition")));
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public async Task AnalyzeAsync_PublishBatchRejection_IsCapturedOnTheResult()
    {
        var store = new RejectingBatchStore();
        var engine = new AnalysisEngine(store, StubStages.CreateDefault());

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create(["orders.slnx"]),
            CancellationToken.None);

        Assert.False(result.HasUnpublishedSolution);
        Assert.True(result.HasBatchPublicationFailure);
        Assert.Equal("batch-composition", result.BatchPublicationGate);
        Assert.Equal("root", result.BatchPublicationDetail);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);
    }

    private static IReadOnlyDictionary<string, byte[]> SnapshotPackages(string outputRoot)
    {
        if (!Directory.Exists(outputRoot))
        {
            return new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        return Directory.GetDirectories(outputRoot)
            .Where(static path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), "^s-[0-9a-f]{32}$"))
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => (
                    Key: Path.GetRelativePath(outputRoot, path).Replace('\\', '/'),
                    Bytes: File.ReadAllBytes(path))))
            .ToDictionary(pair => pair.Key, pair => pair.Bytes, StringComparer.Ordinal);
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

    private sealed class RecordingBatchStore : ITransactionalStore
    {
        private readonly ITransactionalStore _inner;

        public RecordingBatchStore(ITransactionalStore inner) => _inner = inner;

        public ImmutableArray<BatchSolutionRecord> Published { get; private set; }

        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader) =>
            _inner.Open(solutionKey, sourceReader);

        public void PublishBatch(ImmutableArray<BatchSolutionRecord> solutions)
        {
            Published = solutions;
            _inner.PublishBatch(solutions);
        }
    }

    private sealed class SnapshottingBatchStore : ITransactionalStore
    {
        private readonly FilesystemTransactionalStore _inner;
        private readonly string _outputRoot;

        public SnapshottingBatchStore(FilesystemTransactionalStore inner, string outputRoot)
        {
            _inner = inner;
            _outputRoot = outputRoot;
        }

        public ImmutableArray<BatchSolutionRecord> Published { get; private set; }

        public IReadOnlyDictionary<string, byte[]>? PackagesBeforeBatch { get; private set; }

        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader) =>
            _inner.Open(solutionKey, sourceReader);

        public void PublishBatch(ImmutableArray<BatchSolutionRecord> solutions)
        {
            Published = solutions;
            PackagesBeforeBatch = SnapshotPackages(_outputRoot);
            _inner.PublishBatch(solutions);
        }
    }

    private sealed class RejectingBatchStore : ITransactionalStore
    {
        private readonly InMemoryTransactionalStore _inner = new();

        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader) =>
            _inner.Open(solutionKey, sourceReader);

        public void PublishBatch(ImmutableArray<BatchSolutionRecord> solutions) =>
            throw new PublicationRejectedException("batch-composition", "root");
    }
}
