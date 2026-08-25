using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class CanonicalResultOrderTests
{
    [Fact]
    [Trait("Requirement", "ENG-34")]
    [Trait("Requirement", "ROSE-59")]
    public async Task AnalyzeAsync_ShuffledInput_OrdersOutcomesByNormalizedLogicalPath()
    {
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store, StubStages.CreateDefault());

        var shuffled = await engine.AnalyzeAsync(
            AnalysisRequest.Create([@"zeta\b.sln", "alpha/a.sln"]),
            CancellationToken.None);
        var restored = await engine.AnalyzeAsync(
            AnalysisRequest.Create(["alpha/a.sln", @"zeta\b.sln"]),
            CancellationToken.None);

        Assert.Equal(2, shuffled.Solutions.Length);
        Assert.Equal(2, restored.Solutions.Length);
        Assert.Equal(["alpha/a.sln", "zeta/b.sln"], shuffled.Solutions.Select(outcome => outcome.LogicalRelativePath));
        Assert.Equal(["alpha/a.sln", "zeta/b.sln"], restored.Solutions.Select(outcome => outcome.LogicalRelativePath));

        AssertEqualOutcome(shuffled.Solutions[0], restored.Solutions[0]);
        AssertEqualOutcome(shuffled.Solutions[1], restored.Solutions[1]);

        Assert.Equal(PublicationStatus.Committed, shuffled.Solutions[0].Status);
        Assert.Equal(PublicationStatus.Committed, shuffled.Solutions[1].Status);
        Assert.Equal(Path.GetFullPath("alpha/a.sln"), shuffled.Solutions[0].SolutionPath);
        Assert.Equal(Path.GetFullPath(@"zeta\b.sln"), shuffled.Solutions[1].SolutionPath);
        Assert.False(shuffled.Solutions[0].StructuralCorruption);
        Assert.False(shuffled.Solutions[1].HasUnknownsOrCandidatesOrFrontiers);
        Assert.Null(shuffled.Solutions[0].FailingStage);
        Assert.True(store.TryGetPublication(shuffled.Solutions[0].SolutionPath, out var publicationA));
        Assert.True(store.TryGetPublication(shuffled.Solutions[1].SolutionPath, out var publicationB));
        Assert.Equal(ArtifactRole.Manifest, publicationA.ArtifactsInPublicationOrder[^1].Role);
        Assert.Equal(ArtifactRole.Manifest, publicationB.ArtifactsInPublicationOrder[^1].Role);
        Assert.NotEqual(publicationA.SolutionKey, publicationB.SolutionKey);
    }

    private static void AssertEqualOutcome(SolutionOutcome left, SolutionOutcome right)
    {
        Assert.Equal(left.LogicalRelativePath, right.LogicalRelativePath);
        Assert.Equal(left.SolutionPath, right.SolutionPath);
        Assert.Equal(left.Status, right.Status);
        Assert.Equal(left.FailingStage, right.FailingStage);
        Assert.Equal(left.StructuralCorruption, right.StructuralCorruption);
        Assert.Equal(left.HasUnknownsOrCandidatesOrFrontiers, right.HasUnknownsOrCandidatesOrFrontiers);
        Assert.Equal(left.Stages.Length, right.Stages.Length);
        for (var index = 0; index < left.Stages.Length; index++)
        {
            Assert.Equal(left.Stages[index].Name, right.Stages[index].Name);
            Assert.Equal(left.Stages[index].FactCount, right.Stages[index].FactCount);
            Assert.Equal(left.Stages[index].ObservationCount, right.Stages[index].ObservationCount);
            Assert.Equal(left.Stages[index].RelationCount, right.Stages[index].RelationCount);
        }
    }
}
