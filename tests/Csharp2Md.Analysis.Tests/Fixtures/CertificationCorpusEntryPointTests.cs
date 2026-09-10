using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-025: reproduces the audit's private-helper entry-point regression
/// (<c>CatalogController.ChangeUriPlaceholder</c>) in the versioned certification corpus, with no
/// dependency on the local eShop clone. The classifier fix that stops promoting the private helper
/// lands in a later phase; this task only proves the regression is present and reproducible in CI.
/// </summary>
public sealed class CertificationCorpusEntryPointTests
{
    [Fact]
    [Trait("Requirement", "GCPC-025")]
    public async Task AnalyzeAsync_CertificationCorpus_InventoriesAllThreeWidgetsControllerCallablesAsSymbols()
    {
        var publication = await AnalyzeCorpusAsync();

        var structural = ReadShard<StructuralFactsShard>(publication, "facts/structural.json");

        AssertHasCallable(structural, "GetWidget");
        AssertHasCallable(structural, "Index");
        AssertHasCallable(structural, "ChangeUriPlaceholder");
    }

    [Fact]
    [Trait("Requirement", "GCPC-025")]
    public async Task AnalyzeAsync_CertificationCorpus_CurrentlyPromotesThePrivateHelperAsAFabricatedEntryPoint()
    {
        var publication = await AnalyzeCorpusAsync();

        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var widgetsEntryPoints = architecture.EntryPoints
            .Where(entry => entry.Symbol.Id.Contains("WidgetsController", StringComparison.Ordinal))
            .ToArray();

        // Documented pre-fix baseline: EntryPointPass currently has no accessibility check, so it
        // promotes every callable declared on a ControllerBase descendant, private helper included.
        // GCPC-020/GCPC-021 close this in a later phase (T17); this count is exactly what T17 inverts.
        Assert.Equal(3, widgetsEntryPoints.Length);
        Assert.Contains(widgetsEntryPoints, entry => entry.Symbol.Id.Contains("GetWidget", StringComparison.Ordinal));
        Assert.Contains(widgetsEntryPoints, entry => entry.Symbol.Id.Contains("Index", StringComparison.Ordinal));
        Assert.Contains(
            widgetsEntryPoints,
            entry => entry.Symbol.Id.Contains("ChangeUriPlaceholder", StringComparison.Ordinal));
    }

    private static void AssertHasCallable(StructuralFactsShard structural, string methodName) =>
        Assert.Contains(
            structural.Symbols,
            symbol => symbol.Identity.Id.Contains("WidgetsController", StringComparison.Ordinal)
                && symbol.Identity.Id.Contains(methodName, StringComparison.Ordinal));

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
