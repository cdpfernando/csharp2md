using Csharp2Md.Analysis;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

public sealed class CertificationCorpusTests
{
    [Fact]
    [Trait("Requirement", "GCPC-117")]
    public async Task AnalyzeAsync_CertificationCorpus_CommitsAPublicationWithAManifest()
    {
        var solutionPath = CertificationCorpusPaths.SolutionPath;
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var manifestFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "manifest.json");
        var manifest = CanonicalJson.Read<ManifestEnvelope>(manifestFragment.Payload.AsSpan());
        Assert.NotEmpty(manifest.Artifacts);
    }
}
