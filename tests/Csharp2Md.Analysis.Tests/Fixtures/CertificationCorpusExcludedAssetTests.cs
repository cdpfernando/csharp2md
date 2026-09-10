using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-027, GCPC-028, GCPC-032, GCPC-033: proves the supported-document policy (T9) applied inside
/// <c>DocumentInventory</c> (T10) fixes the audit's over-inclusive document inventory (finding I4 /
/// D-04) on the versioned certification corpus. A C#-only project carries, alongside its sources, a
/// `.ts`, a `.js` with a `.map`, an image, a `.zip`, a `package-lock.json` and a `.pfx`; the policy
/// excludes all seven files (two of which - `app.js` and `app.js.map` - make up the ".js with a
/// .map" pair the spec describes as one of the six excluded asset classes), while the project's own
/// `.cs` and `.csproj` documents stay accepted. This inverts the pre-fix baseline this file used to
/// record.
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

    private static readonly string[] AcceptedAssetRelativePaths =
    [
        "Certification.WebAssets/WebAssetHost.cs",
        "Certification.WebAssets/Certification.WebAssets.csproj",
    ];

    [Fact]
    [Trait("Requirement", "GCPC-028")]
    public async Task AnalyzeAsync_CertificationCorpus_ExcludesEveryExcludedAssetFromStructuralFacts()
    {
        var publication = await AnalyzeCorpusAsync();

        var structural = ReadShard<StructuralFactsShard>(publication, "facts/structural.json");
        var documentPaths = structural.Documents.Select(document => document.RelativePath).ToHashSet(StringComparer.Ordinal);

        Assert.All(
            ExcludedAssetRelativePaths,
            relativePath => Assert.DoesNotContain(relativePath, documentPaths));
        Assert.All(
            AcceptedAssetRelativePaths,
            relativePath => Assert.Contains(relativePath, documentPaths));
    }

    [Fact]
    [Trait("Requirement", "GCPC-032")]
    public async Task AnalyzeAsync_CertificationCorpus_PublishesNoSourceArtifactForAnyExcludedAsset()
    {
        var publication = await AnalyzeCorpusAsync();

        var sourceKeys = publication.ArtifactsInPublicationOrder
            .Select(artifact => artifact.CanonicalKey)
            .Where(key => key.StartsWith("source/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        // source/ keys are "source/<lowercased project slug>/<RelativePath>" (SourceProjector.ArtifactKey);
        // the WebAssets project slug is "certification.webassets".
        Assert.All(
            ExcludedAssetRelativePaths,
            relativePath => Assert.DoesNotContain("source/certification.webassets/" + relativePath, sourceKeys));
        Assert.All(
            AcceptedAssetRelativePaths,
            relativePath => Assert.Contains("source/certification.webassets/" + relativePath, sourceKeys));
    }

    [Fact]
    [Trait("Requirement", "GCPC-028")]
    public async Task AnalyzeAsync_CertificationCorpus_ManifestNamesNoExcludedAsset()
    {
        var publication = await AnalyzeCorpusAsync();

        var manifest = ReadShard<ManifestEnvelope>(publication, "manifest.json");
        var manifestPaths = manifest.Artifacts.Select(entry => entry.Path).ToArray();

        Assert.All(
            ExcludedAssetRelativePaths,
            relativePath => Assert.DoesNotContain(
                manifestPaths,
                path => path.Contains(relativePath, StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Requirement", "GCPC-033")]
    public async Task AnalyzeAsync_CertificationCorpus_PublishesExactlyOneAggregatedExclusionDiagnostic()
    {
        var publication = await AnalyzeCorpusAsync();

        var diagnostics = ReadShard<DiagnosticsEnvelope>(publication, "diagnostics.json").Records;

        var aggregated = Assert.Single(
            diagnostics,
            record => string.Equals(record.Code, "unsupported-document", StringComparison.Ordinal));
        Assert.Null(aggregated.IdentityOrKey);
        Assert.Contains("7 document(s)", aggregated.Message, StringComparison.Ordinal);
        foreach (var extension in new[] { ".ts", ".js", ".map", ".png", ".zip", ".json", ".pfx" })
        {
            Assert.Contains(extension, aggregated.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-028")]
    public async Task AnalyzeAsync_CertificationCorpus_PublishesNoIndividualUnsupportedDocumentDiagnostic()
    {
        var publication = await AnalyzeCorpusAsync();

        var diagnostics = ReadShard<DiagnosticsEnvelope>(publication, "diagnostics.json").Records;

        Assert.DoesNotContain(
            diagnostics,
            record => string.Equals(record.Code, "unsupported-document", StringComparison.Ordinal)
                && record.IdentityOrKey is not null);
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

        var store = new InMemoryTransactionalStore(new PackageProjector());
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return publication;
    }
}
