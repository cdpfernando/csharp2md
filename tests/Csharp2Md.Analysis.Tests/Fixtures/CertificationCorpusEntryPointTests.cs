using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-025: reproduces the audit's private-helper entry-point regression
/// (<c>CatalogController.ChangeUriPlaceholder</c>) in the versioned certification corpus, with no
/// dependency on the local eShop clone. T17 closes the regression: <see cref="EntryPointPass"/> now
/// requires positive entry capability (<c>SymbolFacet.ExternallyReachable</c>, GCPC-020) before
/// promoting a callable, so the private helper is inventoried but never published as an EntryPoint.
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
    [Trait("Requirement", "GCPC-020")]
    public async Task AnalyzeAsync_CertificationCorpus_PromotesExactlyTheTwoReachableWidgetsActionsNotThePrivateHelper()
    {
        var publication = await AnalyzeCorpusAsync();

        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var widgetsEntryPoints = architecture.EntryPoints
            .Where(entry => entry.Symbol.Id.Contains("WidgetsController", StringComparison.Ordinal))
            .ToArray();

        // Post-fix: only the two externally reachable actions are published. The private static
        // helper (ChangeUriPlaceholder) is inventoried as a Symbol (asserted above) but is never
        // promoted, because it carries no SymbolFacet.ExternallyReachable evidence (GCPC-020).
        Assert.Equal(2, widgetsEntryPoints.Length);
        Assert.Contains(widgetsEntryPoints, entry => entry.Symbol.Id.Contains("GetWidget", StringComparison.Ordinal));
        Assert.Contains(widgetsEntryPoints, entry => entry.Symbol.Id.Contains("Index", StringComparison.Ordinal));
        Assert.DoesNotContain(
            widgetsEntryPoints,
            entry => entry.Symbol.Id.Contains("ChangeUriPlaceholder", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-022")]
    public async Task AnalyzeAsync_CertificationCorpus_IndexConventionalActionStaysPublishedWithMissingRouteDiagnostic()
    {
        var publication = await AnalyzeCorpusAsync();

        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var diagnostics = ReadShard<DiagnosticsEnvelope>(publication, "diagnostics.json");

        // EBC-08 preserved: Index() carries sufficient entry evidence (a real ControllerBase
        // descendant) but no explicit route, so it is still published as an EntryPoint and its
        // missing-route diagnostic is preserved rather than silently dropped.
        Assert.Contains(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("WidgetsController", StringComparison.Ordinal)
                && entry.Symbol.Id.Contains("Index", StringComparison.Ordinal));
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == "missing-route-declaration"
                && record.Message.Contains("Index", StringComparison.Ordinal));
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
