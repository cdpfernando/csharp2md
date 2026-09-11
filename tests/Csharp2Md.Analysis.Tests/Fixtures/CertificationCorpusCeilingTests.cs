using Csharp2Md.Analysis.Tests.Readiness;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-038 (the F1 fix for audit blocker B4): a real `analyze` of the mandatory certification corpus
/// publishes no artifact over the declared per-artifact ceiling. This is the spec's own Independent Test
/// for the "Bounded payloads and measured budgets" story -- run against the real corpus, with no
/// exclusion list other than the remaining fixed one-per-package envelopes whose size is bounded by a
/// fixed metric/reason count. The manifest is included: F6 shards its entry list when necessary.
/// </summary>
public sealed class CertificationCorpusCeilingTests
{
    private static readonly HashSet<string> UnshardableEnvelopeArtifacts = new(StringComparer.Ordinal)
    {
        "contracts/taxonomy-registry.json",
        "coverage.json",
        "diagnostics.json",
        "measurements.json",
        "run-certification.json",
    };

    [Fact]
    [Trait("Requirement", "GCPC-038")]
    public async Task AnalyzeAsync_CertificationCorpus_PublishesNoArtifactOverTheDeclaredCeiling()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-certcorpus-ceiling-");
        try
        {
            var packageDirectory = await LlmReadinessChecklistTests.PublishCertificationCorpusAsync(tree.FullName);

            var manifest = CanonicalJson.Read<ManifestEnvelope>(
                File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json")));
            Assert.NotNull(manifest.Provenance);
            var ceilingBytes = manifest.Provenance.ArtifactCeilingBytes;
            Assert.True(ceilingBytes > 0, "Expected a positive published ceiling.");

            var offenders = new List<(string Key, long Bytes)>();
            foreach (var file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                if (UnshardableEnvelopeArtifacts.Contains(relative))
                {
                    continue;
                }

                var length = new FileInfo(file).Length;
                if (length > ceilingBytes)
                {
                    offenders.Add((relative, length));
                }
            }

            Assert.True(
                offenders.Count == 0,
                "Artifact(s) over the "
                    + $"{ceilingBytes}-byte ceiling: "
                    + string.Join(", ", offenders.Select(static o => $"{o.Key} ({o.Bytes} bytes)")));

            // The two families the audit found egregiously over ceiling (83,371 and 33,684 bytes,
            // GCPC-038's own headline evidence) now split into shards, each fitting the ceiling.
            var structuralShards = Directory.EnumerateFiles(
                Path.Combine(packageDirectory, "facts"), "structural.*.json", SearchOption.TopDirectoryOnly).ToArray();
            var architectureShards = Directory.EnumerateFiles(
                Path.Combine(packageDirectory, "facts"), "architecture.*.json", SearchOption.TopDirectoryOnly).ToArray();
            Assert.True(structuralShards.Length > 1, "Expected facts/structural.json to actually split for the real corpus.");
            Assert.True(architectureShards.Length > 1, "Expected facts/architecture.json to actually split for the real corpus.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-038")]
    [Trait("Requirement", "GCPC-044")]
    public async Task AnalyzeAsync_AcmeOrders_PublishesNoAvoidableArtifactOverTheDeclaredCeiling()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-acmeorders-ceiling-");
        try
        {
            var solutionPath = Path.Combine(
                AnalysisTestPaths.RepoRoot,
                "fixtures",
                "SyntheticSolution",
                "Acme.Orders",
                "Acme.Orders.slnx");
            var store = new FilesystemTransactionalStore(
                tree.FullName,
                new PackageProjector(CeilingCalculator.Derive().CeilingBytes));
            var result = await new AnalysisEngine(store).AnalyzeAsync(
                AnalysisRequest.Create([solutionPath]),
                CancellationToken.None);
            Assert.Equal(PublicationStatus.Committed, Assert.Single(result.Solutions).Status);

            var packageDirectory = Directory.GetDirectories(tree.FullName).Single();
            var manifest = CanonicalJson.Read<ManifestEnvelope>(
                File.ReadAllBytes(Path.Combine(packageDirectory, "manifest.json")));
            Assert.NotNull(manifest.Provenance);
            var ceilingBytes = manifest.Provenance.ArtifactCeilingBytes;

            var offenders = Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
                .Select(file => new
                {
                    Path = Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/'),
                    Bytes = new FileInfo(file).Length,
                })
                .Where(file => file.Bytes > ceilingBytes)
                .ToArray();

            var offender = Assert.Single(offenders);
            Assert.StartsWith("facts/architecture.", offender.Path, StringComparison.Ordinal);
            Assert.True(
                IsSingleRecordShard(Path.Combine(packageDirectory, offender.Path.Replace('/', Path.DirectorySeparatorChar))),
                $"'{offender.Path}' exceeds the ceiling but is not the permitted indivisible single-record shard.");
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
