using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ContractRelationIntegrationTests
{
    [Fact]
    [Trait("Requirement", "EBC-07")]
    [Trait("Requirement", "EBC-21")]
    [Trait("Requirement", "EBC-23")]
    [Trait("Requirement", "EBC-24")]
    public async Task AnalyzeAsync_AcmeOrders_PromotesOrderPlacedContractAndConfirmedRelations()
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
        Assert.True(outcome.Stages[3].FactCount > 0, $"Classification fact count was {outcome.Stages[3].FactCount}.");
        Assert.True(outcome.Stages[3].RelationCount > 0, $"Classification relation count was {outcome.Stages[3].RelationCount}.");
        var source = File.ReadAllText(
            Path.Combine(AnalysisTestPaths.RepoRoot, "src", "Csharp2Md.Analysis", "Pipeline", "PipelineStages.cs"));
        Assert.Contains("new ComponentPass()", source, StringComparison.Ordinal);
        Assert.Contains("new EntryPointPass()", source, StringComparison.Ordinal);
        Assert.Contains("new BoundaryPass()", source, StringComparison.Ordinal);
        Assert.Contains("new ContractPass()", source, StringComparison.Ordinal);
        Assert.Contains("new RelationPass()", source, StringComparison.Ordinal);

        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        var architectureFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "facts/architecture.json");
        var architecture = CanonicalJson.Read<ArchitectureFactsShard>(architectureFragment.Payload.AsSpan());
        var contractFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "facts/contract.json");
        var contracts = CanonicalJson.Read<ContractFactsShard>(contractFragment.Payload.AsSpan());

        var orderPlaced = Assert.Single(
            contracts.Contracts,
            contract => contract.Proof.Value.Contains("Acme.Shared.Contracts.OrderPlaced", StringComparison.Ordinal));
        Assert.DoesNotContain(
            contracts.Contracts,
            contract => contract.Proof.Value.Contains("PaymentProcessed", StringComparison.Ordinal));

        var publishOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "messaging");
        var handlerOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && operation.Symbol.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal)
                && operation.Direction == "inbound"
                && operation.Protocol == "messaging");
        Assert.Contains(
            contracts.ContractBindings,
            binding => binding.Contract.Id == orderPlaced.Identity.Id
                && binding.PayloadRole == "request"
                && binding.Operation.Id == publishOperation.Identity.Id);
        Assert.Contains(
            contracts.ContractBindings,
            binding => binding.Contract.Id == orderPlaced.Identity.Id
                && binding.PayloadRole == "request"
                && binding.Operation.Id == handlerOperation.Identity.Id);

        var usesContractFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/uses-contract.json");
        var usesContract = CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(usesContractFragment.Payload.AsSpan());
        Assert.Contains(
            usesContract,
            relation => relation.Kind == "uses-contract"
                && relation.Source.Id == publishOperation.Identity.Id
                && relation.Target.Id == orderPlaced.Identity.Id
                && relation.Facets.Any(facet => facet.AxisName == "payload-role" && facet.WireValue == "request")
                && relation.DerivedFrom.Length > 0
                && relation.Classifier.Id == "csharp2md.classifier.contract-messaging"
                && relation.Classifier.Version == 1);
        Assert.Contains(
            usesContract,
            relation => relation.Kind == "uses-contract"
                && relation.Source.Id == handlerOperation.Identity.Id
                && relation.Target.Id == orderPlaced.Identity.Id
                && relation.Facets.Any(facet => facet.AxisName == "payload-role" && facet.WireValue == "request"));

        var implementsFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/implements-operation.json");
        var implements = CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(implementsFragment.Payload.AsSpan());
        Assert.Contains(
            implements,
            relation => relation.Kind == "implements-operation"
                && relation.Source.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && relation.Classifier.Id == "csharp2md.classifier.http-inbound"
                && relation.Classifier.Version == 1
                && relation.DerivedFrom.Length > 0
                && relation.AnalysisVariants.Length > 0);
        Assert.Contains(
            implements,
            relation => relation.Kind == "implements-operation"
                && relation.Source.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && relation.Source.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal)
                && relation.Classifier.Id == "csharp2md.classifier.messaging"
                && relation.Classifier.Version == 1
                && relation.DerivedFrom.Length > 0);
    }
}
