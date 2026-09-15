using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Composition;

public sealed class ShippingFixtureClassificationTests
{
    [Fact]
    [Trait("Requirement", "MSC-16")]
    [Trait("Requirement", "MSC-22")]
    public async Task AnalyzeAsync_AcmeShipping_ClassifiesInboundMessagingAndPostShipmentsMatchingOrders()
    {
        var ordersPath = FixtureSolution("Acme.Orders", "Acme.Orders.slnx");
        var shippingPath = FixtureSolution("Acme.Shipping", "Acme.Shipping.slnx");

        var ordersArchitecture = await AnalyzeArchitectureAsync(ordersPath);
        var shippingArchitecture = await AnalyzeArchitectureAsync(shippingPath);

        var ordersOutbound = Assert.Single(
            ordersArchitecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal)
                && operation.Direction == "outbound"
                && operation.Protocol == "messaging"
                && operation.ProtocolOperationKey is not null
                && operation.ProtocolOperationKey.Value.Contains("OrderPlaced", StringComparison.Ordinal));

        var shippingInbound = Assert.Single(
            shippingArchitecture.BoundaryOperations,
            operation => operation.Symbol.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && operation.Symbol.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal)
                && operation.Direction == "inbound"
                && operation.Protocol == "messaging"
                && operation.ProtocolOperationKey is not null);

        Assert.Equal(ordersOutbound.ProtocolOperationKey!.Value, shippingInbound.ProtocolOperationKey!.Value);

        var postShipments = Assert.Single(
            shippingArchitecture.BoundaryOperations,
            operation => operation.Direction == "inbound"
                && operation.Protocol == "http"
                && operation.ProtocolOperationKey is not null
                && operation.ProtocolOperationKey.Value == "POST shipments");
        Assert.Contains("CreateShipment", postShipments.Symbol.Id, StringComparison.Ordinal);
    }

    private static async Task<ArchitectureFactsShard> AnalyzeArchitectureAsync(string solutionPath)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return ShardedFactsReader.Read<ArchitectureFactsShard>(
            publication.ArtifactsInPublicationOrder, "facts/architecture.json");
    }

    private static string FixtureSolution(string folder, string file)
    {
        var path = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution", folder, file);
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }
}
