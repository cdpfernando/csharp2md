using Csharp2Md.Analysis;
using Csharp2Md.Cli;
using Csharp2Md.Projection;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Composition;

public sealed class PartialBatchTests
{
    [Fact]
    [Trait("Requirement", "MSC-09")]
    [Trait("Requirement", "MSC-10")]
    public async Task Analyze_CommittedSolutionsPlusFailingSibling_LeavesCommittedPackageIdenticalAndExit2()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-partial-batch-");
        try
        {
            var orders = CompositionBatch.OrdersSolutionPath();
            var shipping = CompositionBatch.ShippingSolutionPath();
            var failing = WriteUnreadableSolution(tree.FullName, "Acme.Broken.slnx");

            var committed = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "committed"),
                orders,
                shipping);

            var partialOutput = Path.Combine(tree.FullName, "partial");
            var (exitCode, files) = await InvokeCliAsync(partialOutput, orders, shipping, failing);

            Assert.Equal(2, exitCode);

            var committedOrders = PackageNamed(committed.Files, "Acme.Orders.slnx");
            var partialOrders = PackageNamed(files, "Acme.Orders.slnx");
            CompositionBatch.AssertEqualSnapshots(committedOrders, partialOrders);

            var manifest = CanonicalJson.Read<BatchManifestEnvelope>(files["batch-manifest.json"]);
            Assert.False(manifest.Complete);
            Assert.Equal("solution-unpublished", manifest.IncompleteScopeReason);
            var unpublished = Assert.Single(manifest.Solutions, entry => entry.Status == "unpublished");
            Assert.Equal("Acme.Broken.slnx", unpublished.SolutionFileName);
            Assert.Equal("Inventory", unpublished.FailingStage);
            Assert.Equal(2, manifest.Solutions.Count(entry => entry.Status == "committed"));

            var relation = Assert.Single(
                CompositionBatch.ReadArray(files, "composition/cross-solution-relations.json"));
            Assert.NotNull(relation);
            Assert.Equal("targets", CompositionBatch.Text(relation, "kind"));
            Assert.Contains("OrderPlaced", CompositionBatch.Text(relation, "matched_key"), StringComparison.Ordinal);
            var candidate = Assert.Single(
                CompositionBatch.ReadArray(files, "composition/correlation-candidates.json"));
            Assert.NotNull(candidate);
            Assert.Equal("POST shipments", CompositionBatch.Text(candidate, "matched_key"));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "MSC-35")]
    public async Task Analyze_EverySolutionFailing_PublishesIncompleteManifestWithoutCompositionAndExit2()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-failed-batch-");
        try
        {
            var first = WriteUnreadableSolution(tree.FullName, "Acme.Broken.slnx");
            var second = WriteUnreadableSolution(tree.FullName, "Acme.Broken.Sibling.slnx");
            var output = Path.Combine(tree.FullName, "out");
            var (exitCode, files) = await InvokeCliAsync(output, first, second);

            Assert.Equal(2, exitCode);

            var manifest = CanonicalJson.Read<BatchManifestEnvelope>(files["batch-manifest.json"]);
            Assert.False(manifest.Complete);
            Assert.Equal("solution-unpublished", manifest.IncompleteScopeReason);
            Assert.Equal(2, manifest.Solutions.Length);
            Assert.All(manifest.Solutions, entry => Assert.Equal("unpublished", entry.Status));
            Assert.All(manifest.Solutions, entry => Assert.Equal("Inventory", entry.FailingStage));
            Assert.True(manifest.Artifacts.IsDefaultOrEmpty || manifest.Artifacts.Length == 0);
            Assert.False(Directory.Exists(Path.Combine(output, "composition")));
            Assert.DoesNotContain(
                files.Keys,
                key => key.StartsWith("composition/", StringComparison.Ordinal));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static async Task<(int ExitCode, IReadOnlyDictionary<string, byte[]> Files)> InvokeCliAsync(
        string outputRoot,
        params string[] solutionPaths)
    {
        Directory.CreateDirectory(outputRoot);
        var store = new FilesystemTransactionalStore(outputRoot, new PackageProjector(), new BatchComposer());
        var engine = new AnalysisEngine(store);
        var args = new List<string> { "analyze" };
        foreach (var path in solutionPaths)
        {
            args.Add("--solution");
            args.Add(path);
        }

        args.Add("--output");
        args.Add(outputRoot);
        var exitCode = await CommandFactory.InvokeAsync([.. args], engine);
        return (exitCode, CompositionBatch.SnapshotFiles(outputRoot));
    }

    private static string WriteUnreadableSolution(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "<Solution>");
        return path;
    }

    private static IReadOnlyDictionary<string, byte[]> PackageNamed(
        IReadOnlyDictionary<string, byte[]> files,
        string solutionFileName)
    {
        var manifest = CanonicalJson.Read<BatchManifestEnvelope>(files["batch-manifest.json"]);
        var entry = Assert.Single(manifest.Solutions, solution => solution.SolutionFileName == solutionFileName);
        return CompositionBatch.PackageFiles(files, entry.PackageDirectory);
    }
}
