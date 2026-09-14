using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineCancellationTests
{
    [Fact]
    [Trait("Requirement", "APR-06")]
    public async Task AnalyzeAsync_StageThrowsOperationCanceledException_PropagatesCancellation()
    {
        var engine = new AnalysisEngine(
            new InMemoryTransactionalStore(),
            StubStages.CreateDefault().SetItem(
                1,
                new ThrowingStage("Semantic Analysis", new OperationCanceledException("stage cancelled"))));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.AnalyzeAsync(AnalysisRequest.Create(["alpha.sln"]), CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "ENG-18")]
    [Trait("Requirement", "APR-06")]
    public async Task AnalyzeAsync_CancelAfterStageThree_RecordsThreeStagesAndDoesNotCommit()
    {
        using var cts = new CancellationTokenSource();
        var executed = new List<string>();
        var stages = StubStages.DeclaredNames
            .Select((name, index) => (IPipelineStage)new RecordingStage(
                name,
                executed,
                onExecute: index switch
                {
                    0 => static context => context.Session.Stage(FactualSnapshot.Empty),
                    2 => _ => cts.Cancel(),
                    _ => null,
                }))
            .ToImmutableArray();
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store, stages);
        var solutionPath = "alpha.sln";

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), cts.Token);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.Null(outcome.FailingStage);
        Assert.Null(outcome.Detail);
        Assert.True(result.HasUnpublishedSolution);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            executed);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            outcome.Stages.Select(report => report.Name).ToArray());
        Assert.Equal(3, outcome.Stages.Length);
        Assert.False(store.TryGetPublication(Path.GetFullPath(solutionPath), out _));
    }

    [Fact]
    [Trait("Requirement", "STOR-58")]
    [Trait("Requirement", "APR-06")]
    public async Task AnalyzeAsync_CancelWithFilesystemStore_LeavesNoStagingAndKeepsLastPackage()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "csharp2md-fs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);
        try
        {
            const string solutionPath = "alpha.sln";
            var canonical = Path.GetFullPath(solutionPath);
            var store = new FilesystemTransactionalStore(outputPath);
            var seed = store.Open(canonical, EmptySourceDocumentReader.Instance);
            seed.Stage(FactualSnapshot.Empty);
            seed.Commit();
            var child = Path.Combine(outputPath, ChildName(canonical));
            var prior = PackageSnapshot.Capture(child);

            using var cts = new CancellationTokenSource();
            var executed = new List<string>();
            var stages = StubStages.DeclaredNames
                .Select((name, index) => (IPipelineStage)new RecordingStage(
                    name,
                    executed,
                    onExecute: index switch
                    {
                        0 => static context => context.Session.Stage(FactualSnapshot.Empty),
                        2 => _ => cts.Cancel(),
                        _ => null,
                    }))
                .ToImmutableArray();
            var engine = new AnalysisEngine(store, stages);

            var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), cts.Token);

            var outcome = Assert.Single(result.Solutions);
            Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
            Assert.True(result.HasUnpublishedSolution);
            Assert.Equal(
                ["Inventory", "Semantic Analysis", "Observation Extraction"],
                executed);
            Assert.False(Directory.Exists(child + ".staging"));
            Assert.Empty(Directory.EnumerateFileSystemEntries(outputPath, "*.staging"));
            PackageSnapshot.AssertEqual(prior, PackageSnapshot.Capture(child));
        }
        finally
        {
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    private static string ChildName(string solutionKey)
    {
        var identity = SolutionCoordinate.For(solutionKey).Identity.Value;
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
        return "s-" + hex;
    }
}
