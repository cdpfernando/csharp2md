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
        var architectureFragment = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "facts/architecture.json");
        var architecture = CanonicalJson.Read<ArchitectureFactsShard>(architectureFragment.Payload.AsSpan());

        Assert.Contains(
            architecture.Components,
            component => component.Name.Contains("Acme.Orders", StringComparison.Ordinal)
                && component.Name.Contains("Acme.Orders.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(
            architecture.Components,
            component => component.Name.Contains("Acme.Shared.Contracts", StringComparison.Ordinal));
        Assert.DoesNotContain(
            architecture.Components,
            component => component.Name.Contains("Acme.Broken", StringComparison.Ordinal));

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
