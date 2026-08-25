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
    [Trait("Requirement", "ENG-18")]
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
    public async Task AnalyzeAsync_CancelWithFilesystemStore_LeavesNoStagingAndKeepsLastPackage()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "csharp2md-fs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);
        try
        {
            const string solutionPath = "alpha.sln";
            var canonical = Path.GetFullPath(solutionPath);
            var store = new FilesystemTransactionalStore(outputPath);
            var seed = store.Open(canonical);
            seed.Stage(FactualSnapshot.Empty);
            seed.Commit();
            var child = Path.Combine(outputPath, ChildName(canonical));
            var prior = SnapshotFiles(child);

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
            var after = SnapshotFiles(child);
            Assert.Equal(prior.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var key in prior.Keys)
            {
                Assert.True(prior[key].AsSpan().SequenceEqual(after[key]), $"Bytes at '{key}' changed.");
            }
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
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(solutionKey)))[..32];
        return "s-" + hex;
    }

    private static IReadOnlyDictionary<string, byte[]> SnapshotFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
}
