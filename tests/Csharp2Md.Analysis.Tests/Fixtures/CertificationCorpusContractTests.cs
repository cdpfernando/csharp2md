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
    [Trait("Requirement", "GCPC-087")]
    [Trait("Requirement", "GCPC-092")]
    public async Task AnalyzeAsync_CertificationCorpus_OrderShippedReachesExactlyOneOfTheFourOutcomes()
    {
        var publication = await AnalyzeCorpusAsync();

        var isContracted = HasFamily(publication, "facts/contract.json")
            && ShardedFactsReader.Read<ContractFactsShard>(publication.ArtifactsInPublicationOrder, "facts/contract.json")
                .Contracts.Any(contract => contract.Proof.Value.Contains("OrderShipped", StringComparison.Ordinal));
        var candidates = ReadShardedArray<CandidateLinkDto>(publication, "relations/candidates.json");
        var isCandidate = candidates.Any(link =>
            link.Kind == "uses-contract" && ReferencesOrderShipped(link.DerivedFrom));
        var unresolved = ReadShardedArray<UnresolvedRecordDto>(publication, "relations/unresolved.json");
        var unresolvedMatches = unresolved
            .Where(record => record.Kind == "uses-contract" && ReferencesOrderShipped(record.Available))
            .ToArray();

        // T4's OrderShipped has zero handlers anywhere in the corpus, so it can never become a Contract
        // (GCPC-090's no-name-similarity rule aside, ContractPass.Execute skips any group with no
        // inbound operations) and no CandidateLink names an implementor either -- the only outcome the
        // taxonomy leaves is an UnresolvedRecord, and exactly one, never zero and never more than one
        // (GCPC-087).
        Assert.False(isContracted, "OrderShipped has no handler anywhere in the corpus and must never become a Contract.");
        Assert.False(isCandidate, "OrderShipped has no candidate implementor to propose.");
        var match = Assert.Single(unresolvedMatches);
        Assert.Equal("NoCandidateFound", match.Cause);
    }

    [Fact]
    [Trait("Requirement", "GCPC-087")]
    public async Task AnalyzeAsync_CertificationCorpus_EveryRecognizedPublishOperationReachesExactlyOneOutcome()
    {
        var publication = await AnalyzeCorpusAsync();

        var messageOperations = ReadShard<ImmutableArray<ObservationDto>>(publication, "observations/message-operation.json");
        var publishOperations = messageOperations
            .Where(observation => observation.Identity.Payload.Any(entry =>
                entry.Key == "method-name" && (entry.Value.Value == "PublishAsync" || entry.Value.Value == "Publish")))
            .ToArray();
        Assert.NotEmpty(publishOperations);

        var contractedTypeNames = HasFamily(publication, "facts/contract.json")
            ? ShardedFactsReader.Read<ContractFactsShard>(publication.ArtifactsInPublicationOrder, "facts/contract.json")
                .Contracts.Select(static contract => contract.Proof.Value).ToHashSet(StringComparer.Ordinal)
            : [];
        var candidates = ReadShardedArray<CandidateLinkDto>(publication, "relations/candidates.json");
        var unresolved = ReadShardedArray<UnresolvedRecordDto>(publication, "relations/unresolved.json");

        foreach (var operation in publishOperations)
        {
            var typeArgument = operation.Identity.Payload
                .FirstOrDefault(entry => entry.Key == "type-argument")?.Value.Value;

            var contracted = typeArgument is not null && contractedTypeNames.Contains(typeArgument);
            var candidateCount = candidates.Count(link =>
                link.Kind == "uses-contract" && link.DerivedFrom.Any(evidence => IsSameOccurrence(evidence, operation)));
            var unresolvedCount = unresolved.Count(record =>
                record.Kind == "uses-contract" && record.Available.Any(evidence => IsSameOccurrence(evidence, operation)));

            var outcomes = (contracted ? 1 : 0) + candidateCount + unresolvedCount;
            Assert.True(
                outcomes >= 1,
                $"Message operation on '{operation.Identity.Owner.Id}' (occurrence {operation.Identity.OccurrenceOrdinal}) "
                    + "reached none of contract binding, candidate or unresolved record.");
        }
    }

    private static bool IsSameOccurrence(ObservationIdentityDto evidence, ObservationDto operation) =>
        evidence.Owner.Id == operation.Identity.Owner.Id
        && evidence.Kind == operation.Identity.Kind
        && evidence.OccurrenceOrdinal == operation.Identity.OccurrenceOrdinal;

    private static bool ReferencesOrderShipped(ImmutableArray<ObservationIdentityDto> evidence) =>
        evidence.Any(observation => observation.Owner.Id.Contains("PublishOrderShippedAsync", StringComparison.Ordinal));

    private static bool HasFamily(CommittedPublication publication, string baseKey) =>
        publication.ArtifactsInPublicationOrder.Any(artifact => ShardedFactsReader.IsFamilyMember(artifact.CanonicalKey, baseKey));

    private static ImmutableArray<T> ReadShardedArray<T>(CommittedPublication publication, string canonicalKey)
    {
        var stem = canonicalKey.EndsWith(".json", StringComparison.Ordinal) ? canonicalKey[..^".json".Length] : canonicalKey;
        var shardKeys = publication.ArtifactsInPublicationOrder
            .Select(static artifact => artifact.CanonicalKey)
            .Where(key => key == canonicalKey
                || (key.StartsWith(stem + ".", StringComparison.Ordinal) && key.EndsWith(".json", StringComparison.Ordinal)))
            .OrderBy(static key => key, StringComparer.Ordinal);

        var records = ImmutableArray.CreateBuilder<T>();
        foreach (var key in shardKeys)
        {
            var fragment = publication.ArtifactsInPublicationOrder.Single(artifact => artifact.CanonicalKey == key);
            records.AddRange(CanonicalJson.Read<ImmutableArray<T>>(fragment.Payload.AsSpan()));
        }

        return records.ToImmutable();
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
        ShardedFactsReader.Read<T>(publication.ArtifactsInPublicationOrder, canonicalKey);

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
