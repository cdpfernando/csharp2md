using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-087, GCPC-090: reproduces the audit's contract-accounting gap (finding I2) in the versioned
/// certification corpus: a published event with a handler in the same solution, a published event
/// with no handler, and two same-named payload types in unrelated projects.
/// </summary>
public sealed class CertificationCorpusContractTests
{
    [Fact]
    [Trait("Requirement", "GCPC-087")]
    public async Task AnalyzeAsync_CertificationCorpus_BothPublishedEventsYieldMessageOperationObservations()
    {
        var publication = await AnalyzeCorpusAsync();

        var messageOperations = ReadShard<ImmutableArray<ObservationDto>>(publication, "observations/message-operation.json");

        Assert.Contains(
            messageOperations,
            observation => observation.Identity.Payload.Any(entry =>
                entry.Key == "type-argument" && entry.Value.Value.Contains("OrderCreated", StringComparison.Ordinal)));
        Assert.Contains(
            messageOperations,
            observation => observation.Identity.Payload.Any(entry =>
                entry.Key == "type-argument" && entry.Value.Value.Contains("OrderShipped", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Requirement", "GCPC-090")]
    public async Task AnalyzeAsync_CertificationCorpus_SameNamedPayloadTypesAreDistinctSymbolsInDifferentProjects()
    {
        var publication = await AnalyzeCorpusAsync();

        var structural = ReadShard<StructuralFactsShard>(publication, "facts/structural.json");
        var receipts = structural.Symbols
            .Where(symbol => symbol.CanonicalSymbolSignature.Contains("kind=namedtype", StringComparison.Ordinal)
                && symbol.CanonicalSymbolSignature.Contains(".Receipt", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, receipts.Length);
        Assert.NotEqual(receipts[0].Identity.Id, receipts[1].Identity.Id);
        Assert.NotEqual(receipts[0].OwningProject, receipts[1].OwningProject);
        Assert.Contains(receipts, symbol => symbol.OwningProject.Contains("Certification.Api", StringComparison.Ordinal));
        Assert.Contains(receipts, symbol => symbol.OwningProject.Contains("Certification.Messaging", StringComparison.Ordinal));
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
