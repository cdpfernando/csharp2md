using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class BoundaryIntegrationTests
{
    [Fact]
    [Trait("Requirement", "EBC-06")]
    [Trait("Requirement", "EBC-10")]
    [Trait("Requirement", "EBC-15")]
    [Trait("Requirement", "EBC-16")]
    [Trait("Requirement", "EBC-17")]
    [Trait("Requirement", "EBC-18")]
    [Trait("Requirement", "EBC-20")]
    [Trait("Requirement", "EBC-33")]
    [Trait("Requirement", "CDC-49")]
    [Trait("Requirement", "CDC-55")]
    public async Task AnalyzeAsync_AcmeOrders_PromotesHttpAndMessagingBoundaries()
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
        Assert.IsType<ClassificationAndPromotionStage>(PipelineStages.CreateDefault()[3]);

        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        var architecture = ShardedFactsReader.Read<ArchitectureFactsShard>(
            publication.ArtifactsInPublicationOrder, "facts/architecture.json");
        var candidatesFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/candidates.json");
        var candidates = CanonicalJson.Read<ImmutableArray<CandidateLinkDto>>(candidatesFragment.Payload.AsSpan());

        var getOrderStatus = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && operation.Direction == "inbound"
                && operation.Protocol == "http");
        Assert.Equal("GET orders/{id}", getOrderStatus.ProtocolOperationKey?.Value);
        Assert.Contains("{id}", getOrderStatus.ProtocolOperationKey?.Value, StringComparison.Ordinal);

        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "http"
                && operation.DestinationScope == "PaymentService"
                && operation.HttpMethod == "POST"
                && operation.Route?.Value == "payments/authorize");
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "messaging"
                && operation.ProtocolOperationKey is not null
                && operation.ProtocolOperationKey.Value.Contains("OrderPlaced", StringComparison.Ordinal));
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("NotifyOrderPlacedAsync", StringComparison.Ordinal)
                && operation.DestinationScope == "NotificationService");
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("RequestShippingAsync", StringComparison.Ordinal)
                && operation.DestinationScope == "ShippingService");
        Assert.Contains(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && operation.Symbol.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal)
                && operation.Direction == "inbound"
                && operation.Protocol == "messaging"
                && operation.ProtocolOperationKey is not null
                && operation.ProtocolOperationKey.Value.Contains("OrderPlaced", StringComparison.Ordinal));

        var paymentOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "http"
                && operation.DestinationScope == "PaymentService");
        var notificationOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("NotifyOrderPlacedAsync", StringComparison.Ordinal)
                && operation.DestinationScope == "NotificationService");
        var shippingOperation = Assert.Single(
            architecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("RequestShippingAsync", StringComparison.Ordinal)
                && operation.DestinationScope == "ShippingService");

        Assert.Contains(architecture.ExternalSystems, system => system.Name.Value == "PaymentService" && system.Name.Role == "ClientName");
        Assert.Contains(architecture.ExternalSystems, system => system.Name.Value == "NotificationService" && system.Name.Role == "ClientName");
        Assert.Contains(architecture.ExternalSystems, system => system.Name.Value == "ShippingService" && system.Name.Role == "ClientName");

        var targetCandidates = candidates.Where(link => link.Kind == "targets").ToArray();
        Assert.Equal(2, targetCandidates.Length);
        Assert.Contains(targetCandidates, link => link.Source.Id == notificationOperation.Identity.Id);
        Assert.Contains(targetCandidates, link => link.Source.Id == shippingOperation.Identity.Id);
        Assert.DoesNotContain(targetCandidates, link => link.Source.Id == paymentOperation.Identity.Id);

        var confirmedTargetsFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/targets.json");
        var confirmedTargets = CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(confirmedTargetsFragment.Payload.AsSpan());
        var confirmedTarget = Assert.Single(confirmedTargets, relation => relation.Kind == "targets");
        Assert.Equal(paymentOperation.Identity.Id, confirmedTarget.Source.Id);
    }
}
