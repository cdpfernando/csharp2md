using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PersistenceManifestTests
{
    [Fact]
    [Trait("Requirement", "ENG-23")]
    public async Task AnalyzeAsync_CommittedPublication_EndsWithAManifestFragment()
    {
        var publication = await PublishDefaultPipeline();

        Assert.NotEmpty(publication.ArtifactsInPublicationOrder);
        Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role);
    }

    [Fact]
    [Trait("Requirement", "STOR-50")]
    public async Task AnalyzeAsync_DefaultPipeline_PublishesSchemaValidEmptyPackage()
    {
        var publication = await PublishDefaultPipeline();
        var artifacts = publication.ArtifactsInPublicationOrder;

        Assert.Equal(ArtifactRole.Manifest, artifacts[^1].Role);
        Assert.Equal("manifest.json", artifacts[^1].CanonicalKey);
        Assert.All(artifacts[..^1], fragment => Assert.Equal(ArtifactRole.Payload, fragment.Role));

        var payloadKeys = artifacts[..^1].Select(fragment => fragment.CanonicalKey).ToArray();
        Assert.DoesNotContain(payloadKeys, key => key.StartsWith("facts/", StringComparison.Ordinal));
        Assert.DoesNotContain(payloadKeys, key => key.StartsWith("observations/", StringComparison.Ordinal));
        Assert.DoesNotContain(payloadKeys, key => key.StartsWith("relations/", StringComparison.Ordinal));

        var registry = Assert.Single(artifacts, fragment => fragment.CanonicalKey == "contracts/taxonomy-registry.json");
        var expectedRegistry = DomainMapper.ToWire(FactualSnapshot.Empty, new ManifestContext("s", "s")).TaxonomyRegistryCopy;
        Assert.True(registry.Payload.AsSpan().SequenceEqual(expectedRegistry.AsSpan()));

        var manifest = CanonicalJson.Read<ManifestEnvelope>(artifacts[^1].Payload.AsSpan());
        Assert.All(manifest.Artifacts, entry => Assert.Equal(0, entry.Count));
        Assert.Equal(publication.SolutionKey, manifest.SolutionKey);

        var coverage = Assert.Single(artifacts, fragment => fragment.CanonicalKey == "coverage.json");
        CanonicalJson.Read<CoverageEnvelope>(coverage.Payload.AsSpan());
        var certification = Assert.Single(artifacts, fragment => fragment.CanonicalKey == "run-certification.json");
        Assert.Equal("not_evaluated", CanonicalJson.Read<RunCertificationEnvelope>(certification.Payload.AsSpan()).Status);
    }

    private static async Task<CommittedPublication> PublishDefaultPipeline()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store);

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);

        var sessionKey = Path.GetFullPath(solutionPath);
        Assert.True(store.TryGetPublication(sessionKey, out var publication));
        Assert.Equal(sessionKey, publication.SolutionKey);
        return publication;
    }
}
