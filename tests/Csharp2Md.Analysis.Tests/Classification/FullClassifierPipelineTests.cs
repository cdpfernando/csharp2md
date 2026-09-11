using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class FullClassifierPipelineTests
{
    [Fact]
    [Trait("Requirement", "EBC-35")]
    [Trait("Requirement", "EBC-36")]
    public async Task AnalyzeAsync_AcmeOrders_ClassificationReportsFactsAndRelationsWhileLaterStubsStayZero()
    {
        var (outcome, _) = await AnalyzeAcmeOrdersAsync();

        var classification = outcome.Stages[3];
        Assert.Equal("Classification and Promotion", classification.Name);
        Assert.True(classification.FactCount > 0, $"Classification fact count was {classification.FactCount}.");
        Assert.True(classification.RelationCount > 0, $"Classification relation count was {classification.RelationCount}.");

        AssertZeroProduction(outcome.Stages[4], "Validation and Coverage");
        AssertZeroProduction(outcome.Stages[6], "Retrieval Projection");
        AssertZeroProduction(outcome.Stages[7], "Batch Composition");
    }

    [Fact]
    [Trait("Requirement", "EBC-15")]
    [Trait("Requirement", "EBC-37")]
    public async Task AnalyzeAsync_AcmeOrders_PersistenceStagesClassifierFactsRelationsCandidatesAndUnresolvedSection()
    {
        var (outcome, publication) = await AnalyzeAcmeOrdersAsync();

        Assert.Equal("Persistence", outcome.Stages[5].Name);
        Assert.Equal(0, outcome.Stages[5].FactCount);
        Assert.Equal(0, outcome.Stages[5].ObservationCount);
        Assert.Equal(0, outcome.Stages[5].RelationCount);

        var architecture = ReadShard<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var contracts = ReadShard<ContractFactsShard>(publication, "facts/contract.json");
        Assert.NotEmpty(architecture.Components);
        Assert.NotEmpty(architecture.EntryPoints);
        Assert.NotEmpty(architecture.BoundaryOperations);
        Assert.NotEmpty(contracts.Contracts);

        Assert.Contains(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && entry.Symbol.Id.Contains("OrdersController", StringComparison.Ordinal));
        Assert.Contains(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && entry.Symbol.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal));
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && operation.Direction == "inbound"
                && operation.Protocol == "http");
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "http");
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "messaging");
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && operation.Symbol.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal)
                && operation.Direction == "inbound"
                && operation.Protocol == "messaging");

        var orderPlaced = Assert.Single(
            contracts.Contracts,
            contract => contract.Proof.Value.Contains("Acme.Shared.Contracts.OrderPlaced", StringComparison.Ordinal));
        Assert.Contains(
            contracts.ContractBindings,
            binding => binding.Contract.Id == orderPlaced.Identity.Id && binding.PayloadRole == "request");

        var implements = ReadRelations(publication, "relations/confirmed/implements-operation.json");
        var usesContract = ReadRelations(publication, "relations/confirmed/uses-contract.json");
        Assert.NotEmpty(implements);
        Assert.Contains(implements, relation => relation.Kind == "implements-operation");
        Assert.Contains(
            usesContract,
            relation => relation.Kind == "uses-contract" && relation.Target.Id == orderPlaced.Identity.Id);

        var candidatesFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/candidates.json");
        var candidates = CanonicalJson.Read<ImmutableArray<CandidateLinkDto>>(candidatesFragment.Payload.AsSpan());
        Assert.NotEmpty(candidates);
        Assert.Contains(candidates, link => link.Kind == "targets");
        var outboundHttp = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "http"
                && operation.DestinationScope == "PaymentService");
        Assert.Contains(
            ReadRelations(publication, "relations/confirmed/targets.json"),
            relation => relation.Kind == "targets" && relation.Source.Id == outboundHttp.Identity.Id);
        Assert.DoesNotContain(
            candidates,
            link => link.Kind == "targets" && link.Source.Id == outboundHttp.Identity.Id);

        var manifest = PublishedManifestTestData.Read(publication);
        var unresolvedEntry = Assert.Single(manifest.Artifacts, entry => entry.CanonicalKey == "relations/unresolved");
        var unresolvedFragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == "relations/unresolved.json");
        if (unresolvedFragment is null)
        {
            Assert.Equal(0, unresolvedEntry.Count);
        }
        else
        {
            var unresolved = CanonicalJson.Read<ImmutableArray<UnresolvedRecordDto>>(unresolvedFragment.Payload.AsSpan());
            Assert.Equal(unresolved.Length, unresolvedEntry.Count);
            Assert.Contains(unresolved, record => record.Kind == "operates-on");
        }
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAcmeOrdersAsync()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.Equal(8, outcome.Stages.Length);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static void AssertZeroProduction(StageReport report, string name)
    {
        Assert.Equal(name, report.Name);
        Assert.Equal(0, report.FactCount);
        Assert.Equal(0, report.ObservationCount);
        Assert.Equal(0, report.RelationCount);
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey) =>
        ShardedFactsReader.Read<T>(publication.ArtifactsInPublicationOrder, canonicalKey);

    private static ImmutableArray<ConfirmedRelationDto> ReadRelations(
        CommittedPublication publication,
        string canonicalKey) =>
        CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == canonicalKey).Payload.AsSpan());
}
