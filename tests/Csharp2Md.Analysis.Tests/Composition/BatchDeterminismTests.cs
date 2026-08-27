using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Composition;

public sealed class BatchDeterminismTests
{
    [Fact]
    [Trait("Requirement", "MSC-05")]
    public async Task Analyze_SameSolutionsFromTwoParents_PublishIdenticalPackageNamesAndBatchBytes()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-batch-clone-");
        try
        {
            var cloneA = Path.Combine(tree.FullName, "parent-a");
            var cloneB = Path.Combine(tree.FullName, "parent-b");
            CompositionBatch.CopyFixtureClone(cloneA);
            CompositionBatch.CopyFixtureClone(cloneB);
            Assert.NotEqual(Path.GetFullPath(cloneA), Path.GetFullPath(cloneB));

            var outputA = Path.Combine(tree.FullName, "out-a");
            var outputB = Path.Combine(tree.FullName, "out-b");
            var runA = await CompositionBatch.AnalyzeAsync(outputA, CloneSolutions(cloneA));
            var runB = await CompositionBatch.AnalyzeAsync(outputB, CloneSolutions(cloneB));

            var namesA = PackageDirectoryNames(runA.Files["batch-manifest.json"]);
            var namesB = PackageDirectoryNames(runB.Files["batch-manifest.json"]);
            Assert.Equal(2, namesA.Length);
            Assert.Equal(namesA, namesB);
            Assert.Equal(PublishedPackageDirectories(outputA), PublishedPackageDirectories(outputB));

            CompositionBatch.AssertEqualSnapshots(BatchLayer(runA.Files), BatchLayer(runB.Files));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "MSC-06")]
    public async Task Analyze_ReversedSolutionOrder_PublishesByteIdenticalBatchAndComposition()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-batch-order-");
        try
        {
            var orders = CompositionBatch.OrdersSolutionPath();
            var shipping = CompositionBatch.ShippingSolutionPath();
            var forward = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "forward"),
                orders,
                shipping);
            var reversed = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "reversed"),
                shipping,
                orders);

            CompositionBatch.AssertEqualSnapshots(BatchLayer(forward.Files), BatchLayer(reversed.Files));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static string[] CloneSolutions(string cloneRoot) =>
    [
        Path.Combine(cloneRoot, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx"),
        Path.Combine(cloneRoot, "SyntheticSolution", "Acme.Shipping", "Acme.Shipping.slnx"),
    ];

    private static string[] PackageDirectoryNames(byte[] manifestBytes)
    {
        var manifest = CanonicalJson.Read<BatchManifestEnvelope>(manifestBytes);
        return manifest.Solutions
            .Select(static entry => entry.PackageDirectory)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] PublishedPackageDirectories(string outputRoot) =>
        Directory.GetDirectories(outputRoot)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(static name => name.StartsWith("s-", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyDictionary<string, byte[]> BatchLayer(IReadOnlyDictionary<string, byte[]> files) =>
        files
            .Where(static pair =>
                pair.Key == "batch-manifest.json"
                || pair.Key.StartsWith("composition/", StringComparison.Ordinal))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
}
