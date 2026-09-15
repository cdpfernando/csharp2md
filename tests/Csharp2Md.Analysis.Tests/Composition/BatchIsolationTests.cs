using Csharp2Md.Analysis;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Composition;

public sealed class BatchIsolationTests
{
    [Fact]
    [Trait("Requirement", "MSC-24")]
    public async Task Analyze_OrdersAloneVersusOrdersPlusShipping_LeavesTheOrdersPackageByteIdentical()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-batch-isolation-");
        try
        {
            var orders = CompositionBatch.OrdersSolutionPath();
            var shipping = CompositionBatch.ShippingSolutionPath();
            var solo = await CompositionBatch.AnalyzeAsync(Path.Combine(tree.FullName, "solo"), orders);
            var batch = await CompositionBatch.AnalyzeAsync(Path.Combine(tree.FullName, "batch"), orders, shipping);

            var soloOrders = PackageNamed(solo, "Acme.Orders.slnx");
            var batchOrders = PackageNamed(batch, "Acme.Orders.slnx");
            CompositionBatch.AssertEqualSnapshots(soloOrders, batchOrders);

            Assert.DoesNotContain(soloOrders.Keys, static key => key.StartsWith("composition/", StringComparison.Ordinal));
            Assert.DoesNotContain(batchOrders.Keys, static key => key.StartsWith("composition/", StringComparison.Ordinal));
            Assert.Equal(
                KnowledgeKeys(soloOrders),
                KnowledgeKeys(batchOrders));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "MSC-08")]
    public async Task Analyze_SingleSolution_PublishesOneManifestEntryAndNoCrossSolutionArtifacts()
    {
        using var output = TempOutputRoot.Uncreated("csharp2md-batch-single-");

        var run = await CompositionBatch.AnalyzeAsync(output.DirectoryPath, CompositionBatch.OrdersSolutionPath());
        Assert.Equal(PublicationStatus.Committed, Assert.Single(run.Result.Solutions).Status);

        var manifest = CanonicalJson.Read<BatchManifestEnvelope>(run.Files["batch-manifest.json"]);
        Assert.Equal("Acme.Orders.slnx", Assert.Single(manifest.Solutions).SolutionFileName);
        Assert.True(manifest.Complete);

        Assert.False(run.Files.ContainsKey("composition/cross-solution-relations.json"));
        Assert.False(run.Files.ContainsKey("composition/shared-contracts.json"));
        Assert.False(run.Files.ContainsKey("composition/correlation-candidates.json"));
        Assert.DoesNotContain(
            manifest.Artifacts,
            artifact => artifact.CanonicalKey is
                "composition/cross-solution-relations.json"
                or "composition/shared-contracts.json"
                or "composition/correlation-candidates.json");
    }

    private static IReadOnlyDictionary<string, byte[]> PackageNamed(CompositionBatch.BatchRun run, string solutionFileName)
    {
        var manifest = CanonicalJson.Read<BatchManifestEnvelope>(run.Files["batch-manifest.json"]);
        var entry = Assert.Single(manifest.Solutions, solution => solution.SolutionFileName == solutionFileName);
        return CompositionBatch.PackageFiles(run.Files, entry.PackageDirectory);
    }

    private static string[] KnowledgeKeys(IReadOnlyDictionary<string, byte[]> package) =>
        package.Keys
            .Where(static key =>
                key.StartsWith("facts/", StringComparison.Ordinal)
                || key.StartsWith("observations/", StringComparison.Ordinal)
                || key.StartsWith("relations/confirmed/", StringComparison.Ordinal)
                || key.StartsWith("relations/candidates", StringComparison.Ordinal)
                || key.StartsWith("quarantine/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
}
