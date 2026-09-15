using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Composition;
using Csharp2Md.Analysis.Tests.Fixtures;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Determinism;

/// <summary>
/// GCPC-111: a batch containing more than one solution keeps each solution's semantics isolated --
/// no fact identity from a sibling solution enters another solution's package, and running a solution
/// alone versus inside a batch publishes byte-identical package contents. The pre-existing
/// <c>Composition/BatchIsolationTests</c> (MSC-08, MSC-24) already proves the solo-versus-batch byte
/// equality and the absence of cross-solution artifact keys; this suite adds the identity-carrying
/// proof the certification corpus's own batch requires -- every fact ID in one package is scoped to
/// its own solution and never carries the sibling's identity, encoded or raw.
/// </summary>
public sealed class BatchIsolationTests
{
    [Fact]
    [Trait("Requirement", "GCPC-111")]
    public async Task Analyze_CertificationCorpusAndConfigurationShapesBatch_EveryFactCarriesOnlyItsOwnSolutionIdentity()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-batch-iso-identity-");
        try
        {
            var certificationPath = CertificationCorpusPaths.SolutionPath;
            var configurationShapesPath = ConfigurationShapesSolutionPath();

            var run = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "batch"), certificationPath, configurationShapesPath);

            var certificationPackage = PackageNamed(run, "CertificationCorpus.slnx");
            var configurationPackage = PackageNamed(run, "ConfigurationShapes.sln");

            var certificationIdentity = SolutionCoordinate.For(certificationPath).Identity.Value;
            var configurationIdentity = SolutionCoordinate.For(configurationShapesPath).Identity.Value;

            AssertFactsCarryOnlyOwnSolutionIdentity(certificationPackage, certificationIdentity, configurationIdentity);
            AssertFactsCarryOnlyOwnSolutionIdentity(configurationPackage, configurationIdentity, certificationIdentity);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-111")]
    public async Task Analyze_CertificationCorpusAloneVersusInsideABatch_PublishesByteIdenticalPackage()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-batch-iso-bytes-");
        try
        {
            var certificationPath = CertificationCorpusPaths.SolutionPath;
            var configurationShapesPath = ConfigurationShapesSolutionPath();

            var solo = await CompositionBatch.AnalyzeAsync(Path.Combine(tree.FullName, "solo"), certificationPath);
            var batch = await CompositionBatch.AnalyzeAsync(
                Path.Combine(tree.FullName, "batch"), certificationPath, configurationShapesPath);

            var soloPackage = PackageNamed(solo, "CertificationCorpus.slnx");
            var batchPackage = PackageNamed(batch, "CertificationCorpus.slnx");

            CompositionBatch.AssertEqualSnapshots(soloPackage, batchPackage);
            Assert.DoesNotContain(soloPackage.Keys, static key => key.StartsWith("composition/", StringComparison.Ordinal));
            Assert.DoesNotContain(batchPackage.Keys, static key => key.StartsWith("composition/", StringComparison.Ordinal));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Walks every string leaf under every <c>facts/*.json</c> artifact in the package. A canonical
    /// fact identity always begins with the <c>id1:</c> grammar prefix (see <c>FactIdGrammar</c>) and
    /// nests its parent's identity as a percent-encoded component at every level (<c>ProjectId</c>
    /// nests <c>SolutionId</c>, which itself nests <c>WorkspaceIdentity</c>, and so on down to
    /// <c>SymbolDto</c>) -- so a deeply nested fact carries the owning solution's identity encoded
    /// more than once (encoded once per nesting level crossed). Comparing encoded forms directly would
    /// have to know each fact's nesting depth, so both the candidate identity and the two solution
    /// identities being compared against are fully unescaped to a fixed point first, which flattens
    /// every nesting level to its plain, single-encoded form and makes a plain substring check
    /// nesting-depth-independent.
    /// </summary>
    private static void AssertFactsCarryOnlyOwnSolutionIdentity(
        IReadOnlyDictionary<string, byte[]> package,
        string ownSolutionIdentity,
        string siblingSolutionIdentity)
    {
        var ownFlat = FullyUnescape(ownSolutionIdentity);
        var siblingFlat = FullyUnescape(siblingSolutionIdentity);

        var factKeys = package.Keys.Where(static key => key.StartsWith("facts/", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(factKeys);

        var checkedIdentities = 0;
        foreach (var key in factKeys)
        {
            var node = JsonNode.Parse(package[key]);
            Assert.NotNull(node);
            foreach (var identity in CollectFactIdentities(node))
            {
                checkedIdentities++;
                var identityFlat = FullyUnescape(identity);

                Assert.True(
                    identityFlat.Contains(ownFlat, StringComparison.Ordinal),
                    $"'{identity}' at '{key}' does not carry its own solution's identity.");
                Assert.DoesNotContain(siblingFlat, identityFlat, StringComparison.Ordinal);
            }
        }

        Assert.True(checkedIdentities > 0, "Expected at least one fact identity to check.");
    }

    private static string FullyUnescape(string value)
    {
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var next = Uri.UnescapeDataString(value);
            if (string.Equals(next, value, StringComparison.Ordinal))
            {
                return value;
            }

            value = next;
        }

        return value;
    }

    private static IEnumerable<string> CollectFactIdentities(JsonNode? node)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text) && text.StartsWith("id1:", StringComparison.Ordinal):
                yield return text;
                break;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    foreach (var nested in CollectFactIdentities(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    foreach (var nested in CollectFactIdentities(element))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static IReadOnlyDictionary<string, byte[]> PackageNamed(CompositionBatch.BatchRun run, string solutionFileName)
    {
        var manifest = CanonicalJson.Read<BatchManifestEnvelope>(run.Files["batch-manifest.json"]);
        var entry = Assert.Single(manifest.Solutions, solution => solution.SolutionFileName == solutionFileName);
        return CompositionBatch.PackageFiles(run.Files, entry.PackageDirectory);
    }

    private static string ConfigurationShapesSolutionPath()
    {
        var path = Path.Combine(CertificationCorpusPaths.RootPath, "ConfigurationShapes", "ConfigurationShapes.sln");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }
}
