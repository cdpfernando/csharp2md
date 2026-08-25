using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class StructuralCorruptionTests
{
    [Fact]
    [Trait("Requirement", "ENG-24")]
    public async Task AnalyzeAsync_ValidationReportsStructuralCorruption_AbortsAndKeepsPriorPublication()
    {
        var store = new InMemoryTransactionalStore();
        var solutionPath = "alpha.sln";
        var sessionKey = Path.GetFullPath(solutionPath);
        var request = AnalysisRequest.Create([solutionPath]);

        var first = await new AnalysisEngine(store).AnalyzeAsync(request, CancellationToken.None);

        var firstOutcome = Assert.Single(first.Solutions);
        Assert.Equal(PublicationStatus.Committed, firstOutcome.Status);
        Assert.False(firstOutcome.StructuralCorruption);
        Assert.True(store.TryGetPublication(sessionKey, out var prior));
        Assert.Equal(sessionKey, prior.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, prior.ArtifactsInPublicationOrder[^1].Role);

        var executed = new List<string>();
        var corruptedValidation = new ResultStage(
            "Validation and Coverage",
            new StageResult(0, 0, 0, StructuralCorruption: true, HasUnknownsOrCandidatesOrFrontiers: false),
            executed);
        var stages = StubStages.CreateDefault()
            .SetItem(4, corruptedValidation)
            .SetItem(5, new RecordingStage("Persistence", executed))
            .SetItem(6, new RecordingStage("Retrieval Projection", executed))
            .SetItem(7, new RecordingStage("Batch Composition", executed));

        var second = await new AnalysisEngine(store, stages).AnalyzeAsync(request, CancellationToken.None);

        var outcome = Assert.Single(second.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.True(outcome.StructuralCorruption);
        Assert.False(outcome.HasUnknownsOrCandidatesOrFrontiers);
        Assert.Null(outcome.FailingStage);
        Assert.True(second.HasUnpublishedSolution);
        Assert.Equal(["Validation and Coverage"], executed);
        Assert.DoesNotContain("Persistence", outcome.Stages.Select(report => report.Name));
        Assert.DoesNotContain("Retrieval Projection", outcome.Stages.Select(report => report.Name));
        Assert.DoesNotContain("Batch Composition", outcome.Stages.Select(report => report.Name));
        Assert.Equal("Validation and Coverage", outcome.Stages[^1].Name);

        Assert.True(store.TryGetPublication(sessionKey, out var kept));
        Assert.Equal(prior, kept);
        Assert.Equal(prior.SolutionKey, kept.SolutionKey);
        Assert.Equal(prior.ArtifactsInPublicationOrder.Length, kept.ArtifactsInPublicationOrder.Length);
        for (var index = 0; index < prior.ArtifactsInPublicationOrder.Length; index++)
        {
            Assert.Equal(prior.ArtifactsInPublicationOrder[index].Role, kept.ArtifactsInPublicationOrder[index].Role);
            Assert.Equal(
                prior.ArtifactsInPublicationOrder[index].CanonicalKey,
                kept.ArtifactsInPublicationOrder[index].CanonicalKey);
            Assert.True(
                prior.ArtifactsInPublicationOrder[index].Payload.AsSpan()
                    .SequenceEqual(kept.ArtifactsInPublicationOrder[index].Payload.AsSpan()));
        }
    }
}
