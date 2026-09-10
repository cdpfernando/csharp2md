using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-039/044 (partial closure -- real ceiling enforcement is Phase 6's sharding work): proves the
/// per-edge <see cref="Csharp2Md.Analysis.Classification.EvidenceScope"/> fixes in
/// <c>ContainsRelationEmitter</c> (T20) and <c>TopologyEmitter</c> (T21) actually shrink the published
/// <c>contains</c> and <c>belongs-to</c> payloads for the certification corpus, instead of only
/// changing their content.
/// </summary>
public sealed class CertificationCorpusContainsPayloadTests
{
    /// <summary>
    /// Measured against the pre-fix <c>ContainsRelationEmitter</c> (one document-wide evidence chain
    /// reused for every `contains` edge in that document), before T20 adopted <c>EvidenceScope</c>.
    /// </summary>
    private const long PreFixContainsPayloadBytes = 952_956;

    /// <summary>
    /// Measured against the pre-fix <c>TopologyEmitter</c> (every observation the symbol owns, of any
    /// kind, cited on its `belongs-to` edge), before T21 adopted <c>EvidenceScope</c>.
    /// </summary>
    private const long PreFixBelongsToPayloadBytes = 110_874;

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    [Trait("Requirement", "GCPC-044")]
    public async Task AnalyzeAsync_CertificationCorpus_ContainsPayloadIsALargeReductionAgainstThePreFixFigure()
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

        var containsArtifact = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/contains.json");
        var postFixBytes = containsArtifact.Payload.AsSpan().Length;

        Assert.True(
            postFixBytes < PreFixContainsPayloadBytes / 2,
            $"Expected a large reduction: post-fix {postFixBytes} bytes vs pre-fix {PreFixContainsPayloadBytes} bytes.");
    }

    [Fact]
    [Trait("Requirement", "GCPC-039")]
    public async Task AnalyzeAsync_CertificationCorpus_BelongsToPayloadIsReducedAgainstThePreFixFigure()
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

        var belongsToArtifact = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/belongs-to.json");
        var postFixBytes = belongsToArtifact.Payload.AsSpan().Length;

        Assert.True(
            postFixBytes < PreFixBelongsToPayloadBytes,
            $"Expected a reduction: post-fix {postFixBytes} bytes vs pre-fix {PreFixBelongsToPayloadBytes} bytes.");
    }
}
