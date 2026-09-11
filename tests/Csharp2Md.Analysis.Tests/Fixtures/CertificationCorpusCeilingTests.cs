using Csharp2Md.Analysis.Tests.Readiness;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-038 (the F1 fix for audit blocker B4): a real `analyze` of the mandatory certification corpus
/// publishes no artifact over the declared per-artifact ceiling. This is the spec's own Independent Test
/// for the "Bounded payloads and measured budgets" story -- run against the real corpus, with no
/// exclusion list of any kind other than the fixed one-per-package envelope artifacts (manifest,
/// registry, coverage, diagnostics, measurements, run-certification), whose own size is bounded by a
/// fixed metric/reason count or, for manifest.json, by this run's own shard count -- never by any one
/// record's content, so splitting them is a self-referential concern outside GCPC-039's family list, not
/// evidence of the ceiling violation this test exists to catch.
/// </summary>
public sealed class CertificationCorpusCeilingTests
{
    private static readonly HashSet<string> UnshardableEnvelopeArtifacts = new(StringComparer.Ordinal)
    {
        "manifest.json",
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
}
