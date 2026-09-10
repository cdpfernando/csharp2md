using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-028, GCPC-032: reproduces the audit's over-inclusive document inventory (finding I4 / D-04)
/// in the versioned certification corpus: a C#-only project carrying, alongside its sources, a
/// `.ts`, a `.js` with a `.map`, an image, a `.zip`, a `package-lock.json` and a `.pfx`. The
/// supported-document policy that excludes all six is a later phase (T9/T10); this task only proves
/// today's over-inclusive baseline, which that policy inverts.
/// </summary>
public sealed class CertificationCorpusExcludedAssetTests
{
    private static readonly string[] ExcludedAssetRelativePaths =
    [
        "Certification.WebAssets/app.ts",
        "Certification.WebAssets/app.js",
        "Certification.WebAssets/app.js.map",
        "Certification.WebAssets/logo.png",
        "Certification.WebAssets/assets.zip",
        "Certification.WebAssets/package-lock.json",
        "Certification.WebAssets/cert.pfx",
    ];

    [Fact]
    [Trait("Requirement", "GCPC-028")]
    public async Task AnalyzeAsync_CertificationCorpus_CurrentlyInventoriesEveryExcludedAssetAsADocument()
    {
        var publication = await AnalyzeCorpusAsync();

        var structural = ReadShard<StructuralFactsShard>(publication, "facts/structural.json");
        var documentPaths = structural.Documents.Select(document => document.RelativePath).ToHashSet(StringComparer.Ordinal);

        Assert.All(
            ExcludedAssetRelativePaths,
            relativePath => Assert.Contains(relativePath, documentPaths));
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey) =>
        CanonicalJson.Read<T>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == canonicalKey).Payload.AsSpan());

    private static async Task<CommittedPublication> AnalyzeCorpusAsync()
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
        return publication;
    }
}
