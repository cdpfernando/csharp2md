using Csharp2Md.Analysis;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

public sealed class PublicationResilienceCeilingTests
{
    [Fact]
    [Trait("Requirement", "APR-38")]
    [Trait("Requirement", "APR-29")]
    public async Task AnalyzeAsync_Volume_PublishesUnknownAndFrontierPostingShardsUnderTheDerivedCeiling()
    {
        var solutionPath = PublicationResiliencePaths.SolutionPath;
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");
        Assert.True(
            Directory.Exists(Path.Combine(PublicationResiliencePaths.RootPath, "Publication.Lib", "Volume")),
            "Expected versioned volume sources.");

        var derivedCeiling = CeilingCalculator.Derive().CeilingBytes;
        Assert.Equal(32 * 1024, derivedCeiling);

        var tree = Directory.CreateTempSubdirectory("csharp2md-publication-resilience-ceiling-");
        try
        {
            var store = new FilesystemTransactionalStore(
                tree.FullName,
                new PackageProjector(derivedCeiling));
            var result = await new AnalysisEngine(store).AnalyzeAsync(
                AnalysisRequest.Create([solutionPath]),
                CancellationToken.None);
            var outcome = Assert.Single(result.Solutions);
            Assert.True(
                outcome.Status == PublicationStatus.Committed,
                $"{outcome.Status} stage={outcome.FailingStage} detail={outcome.Detail}");

            var packageDirectory = Directory.GetDirectories(tree.FullName).Single();
            var relativeKeys = Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/'))
                .ToArray();

            Assert.DoesNotContain("postings/unknowns.json", relativeKeys);
            Assert.DoesNotContain("postings/frontiers.json", relativeKeys);
            Assert.Contains(
                relativeKeys,
                key => key.StartsWith("postings/unknowns.", StringComparison.Ordinal)
                    && key.EndsWith(".json", StringComparison.Ordinal));
            Assert.Contains(
                relativeKeys,
                key => key.StartsWith("postings/frontiers.", StringComparison.Ordinal)
                    && key.EndsWith(".json", StringComparison.Ordinal));
            Assert.Contains("retrieval.md", relativeKeys);

            var manifest = CanonicalJson.Read<ManifestEnvelope>(
                File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json")));
            Assert.NotNull(manifest.Provenance);
            Assert.Equal(derivedCeiling, manifest.Provenance.ArtifactCeilingBytes);

            var offenders = Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
                .Select(file => new
                {
                    Path = Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/'),
                    Bytes = new FileInfo(file).Length,
                    FullPath = file,
                })
                .Where(file => file.Bytes > derivedCeiling)
                .ToArray();
            Assert.All(
                offenders,
                offender => Assert.True(
                    IsSingleRecordShard(offender.FullPath),
                    $"'{offender.Path}' ({offender.Bytes} bytes) exceeds the {derivedCeiling}-byte derived ceiling and is not the permitted indivisible single-record shard."));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static bool IsSingleRecordShard(string path)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(path));
        return node switch
        {
            System.Text.Json.Nodes.JsonArray array => array.Count == 1,
            System.Text.Json.Nodes.JsonObject { Count: > 0 } obj
                when obj.All(static property => property.Value is System.Text.Json.Nodes.JsonArray) =>
                obj.Sum(static property => ((System.Text.Json.Nodes.JsonArray)property.Value!).Count) == 1,
            _ => false,
        };
    }
}
