using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ComponentEntryPointIntegrationTests
{
    [Fact]
    [Trait("Requirement", "EBC-01")]
    [Trait("Requirement", "EBC-02")]
    [Trait("Requirement", "EBC-05")]
    [Trait("Requirement", "EBC-19")]
    [Trait("Requirement", "EBC-27")]
    [Trait("Requirement", "EBC-35")]
    [Trait("Requirement", "CDC-09")]
    [Trait("Requirement", "CDC-11")]
    public async Task AnalyzeAsync_AcmeOrders_PromotesComponentAndEntryPoints()
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
        var classification = outcome.Stages[3];
        Assert.Equal("Classification and Promotion", classification.Name);
        Assert.True(classification.FactCount > 0, $"Classification fact count was {classification.FactCount}.");
        Assert.IsType<ClassificationAndPromotionStage>(PipelineStages.CreateDefault()[3]);

        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        var architecture = ShardedFactsReader.Read<ArchitectureFactsShard>(
            publication.ArtifactsInPublicationOrder, "facts/architecture.json");

        Assert.Equal(
            new[]
            {
                "Acme.Orders/Acme.Orders.csproj",
                "Acme.Orders.Worker/Acme.Orders.Worker.csproj",
                "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj",
            }.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            architecture.Components.Select(component => component.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(
            architecture.Components,
            component => component.Name == "Acme.Broken/Acme.Broken.csproj");

        Assert.Contains(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && entry.Symbol.Id.Contains("OrdersController", StringComparison.Ordinal));
        Assert.Contains(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && entry.Symbol.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal));
        Assert.DoesNotContain(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("PlaceOrderAsync", StringComparison.Ordinal));
    }
}
