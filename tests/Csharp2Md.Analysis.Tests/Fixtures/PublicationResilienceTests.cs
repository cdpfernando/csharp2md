using Csharp2Md.Analysis;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

public sealed class PublicationResilienceTests
{
    [Fact]
    [Trait("Requirement", "APR-33")]
    public async Task AnalyzeAsync_Skeleton_CommitsAPublicationWithAManifest()
    {
        var solutionPath = PublicationResiliencePaths.SolutionPath;
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");
        Assert.Equal(
            Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "PublicationResilience"),
            PublicationResiliencePaths.RootPath);
        Assert.NotEqual(CertificationCorpusPaths.RootPath, PublicationResiliencePaths.RootPath);
        Assert.False(
            Directory.Exists(Path.Combine(PublicationResiliencePaths.RootPath, "labels")),
            "PublicationResilience must stay unlabeled.");

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
