using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-039/044 (partial closure -- real ceiling enforcement is Phase 6's sharding work): proves the
/// per-edge <see cref="Csharp2Md.Analysis.Classification.EvidenceScope"/> fix in
/// <c>ContainsRelationEmitter</c> (T20) actually shrinks the published <c>contains</c> payload for the
/// certification corpus, instead of only changing its content.
/// </summary>
public sealed class CertificationCorpusContainsPayloadTests
{
    /// <summary>
    /// Measured against the pre-fix <c>ContainsRelationEmitter</c> (one document-wide evidence chain
    /// reused for every `contains` edge in that document), before T20 adopted <c>EvidenceScope</c>.
    /// </summary>
    private const long PreFixContainsPayloadBytes = 952_956;

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
}
