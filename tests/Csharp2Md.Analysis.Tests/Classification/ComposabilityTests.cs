using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ComposabilityTests
{
    [Fact]
    [Trait("Requirement", "EBC-27")]
    [Trait("Requirement", "EBC-28")]
    [Trait("Requirement", "EBC-29")]
    [Trait("Requirement", "EBC-30")]
    public async Task AnalyzeAsync_CountingPassRegisteredAfter5A_SeesPriorFactsAndAddsToAggregateCounts()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var baseline = await AnalyzeAsync(solutionPath, PipelineStages.CreateDefault());

        var order = new List<string>();
        var counting = new CountingClassifierPass(order);
        var stages = PipelineStages.CreateDefault().SetItem(
            3,
            new ClassificationAndPromotionStage(
            [
                new RecordingPass(new ComponentPass(), order),
                new RecordingPass(new EntryPointPass(), order),
                new RecordingPass(new BoundaryPass(), order),
                new RecordingPass(new ContractPass(), order),
                new RecordingPass(new PersistencePass(), order),
                new RecordingPass(new RelationPass(), order),
                new RecordingPass(new InvokesPass(), order),
                new RecordingPass(new ExecutesPass(), order),
                counting,
            ]));
        var composed = await AnalyzeAsync(solutionPath, stages);

        Assert.Equal(
            ["Components", "Entry points", "Boundaries", "Contracts", "Persistence", "Relations", CountingClassifierPass.PassName],
            order);
        Assert.Equal(baseline.Outcome.Stages[3].FactCount + 1, composed.Outcome.Stages[3].FactCount);
        Assert.Equal(baseline.Outcome.Stages[3].RelationCount, composed.Outcome.Stages[3].RelationCount);
        Assert.True(counting.ComponentCount > 0, $"Counting pass saw {counting.ComponentCount} component facts.");
        Assert.True(counting.EntryPointCount > 0, $"Counting pass saw {counting.EntryPointCount} entry-point facts.");
        Assert.Equal(1, counting.ReportedFactCount);

        var architecture = CanonicalJson.Read<ArchitectureFactsShard>(
            Assert.Single(
                composed.Publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "facts/architecture.json").Payload.AsSpan());
        Assert.Contains(architecture.DeploymentUnits, unit => unit.Name == CountingClassifierPass.DummyName);
        Assert.Contains(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("GetOrderStatus", StringComparison.Ordinal));

        var baselineArchitecture = CanonicalJson.Read<ArchitectureFactsShard>(
            Assert.Single(
                baseline.Publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "facts/architecture.json").Payload.AsSpan());
        Assert.Equal(baselineArchitecture.Components.Length, architecture.Components.Length);
        Assert.Equal(baselineArchitecture.EntryPoints.Length, architecture.EntryPoints.Length);
        Assert.Equal(baselineArchitecture.DeploymentUnits.Length + 1, architecture.DeploymentUnits.Length);
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAsync(
        string solutionPath,
        ImmutableArray<IPipelineStage> stages)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store, stages).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private sealed class RecordingPass(IClassifierPass inner, List<string> order) : IClassifierPass
    {
        public string Name => inner.Name;

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            order.Add(Name);
            return inner.Execute(context, cancellationToken);
        }
    }

    private sealed class CountingClassifierPass(List<string> order) : IClassifierPass
    {
        internal const string PassName = "Counting";
        internal const string DummyName = "CountingPass.Dummy";

        public string Name => PassName;

        public int ComponentCount { get; private set; }

        public int EntryPointCount { get; private set; }

        public int ReportedFactCount { get; private set; }

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            order.Add(Name);
            ComponentCount = context.FactsByType<Component>().Length;
            EntryPointCount = context.FactsByType<EntryPoint>().Length;
            context.Accumulator.AddFact(DeploymentUnit.Create(context.SolutionId, DummyName));
            ReportedFactCount = 1;
            return new ClassifierPassResult(1, 0, 0, 0);
        }
    }
}
