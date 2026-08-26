using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ClassificationAndPromotionStageTests
{
    [Fact]
    [Trait("Requirement", "EBC-27")]
    public void Name_IsClassificationAndPromotion()
    {
        var stage = new ClassificationAndPromotionStage([]);

        Assert.Equal("Classification and Promotion", stage.Name);
    }

    [Fact]
    [Trait("Requirement", "EBC-27")]
    [Trait("Requirement", "EBC-30")]
    public async Task ExecuteAsync_NoPasses_ReturnsZeroCounts()
    {
        var pipeline = CreatePipeline();
        var stage = new ClassificationAndPromotionStage([]);

        var result = await stage.ExecuteAsync(pipeline, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Equal(0, result.ObservationCount);
        Assert.Equal(0, result.RelationCount);
        Assert.False(result.HasUnknownsOrCandidatesOrFrontiers);
        Assert.False(result.StructuralCorruption);
    }

    [Fact]
    [Trait("Requirement", "EBC-29")]
    public async Task ExecuteAsync_RunsPassesInRegistrationOrder()
    {
        var pipeline = CreatePipeline();
        var order = new List<string>();
        var stage = new ClassificationAndPromotionStage(
        [
            new RecordingPass("first", order),
            new RecordingPass("second", order),
            new RecordingPass("third", order),
        ]);

        await stage.ExecuteAsync(pipeline, CancellationToken.None);

        Assert.Equal(["first", "second", "third"], order);
    }

    [Fact]
    [Trait("Requirement", "EBC-28")]
    [Trait("Requirement", "EBC-29")]
    public async Task ExecuteAsync_RefreshesContextBetweenPasses()
    {
        var pipeline = CreatePipeline();
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        var seen = new List<int>();
        var stage = new ClassificationAndPromotionStage(
        [
            new ProjectAddingPass(),
            new ProjectCountingPass(seen),
        ]);

        await stage.ExecuteAsync(pipeline, CancellationToken.None);

        Assert.Equal(1, Assert.Single(seen));
        Assert.Equal(Project.Create(OrdersProject), Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Project>().ToArray()));
    }

    [Fact]
    [Trait("Requirement", "EBC-30")]
    public async Task ExecuteAsync_ReportsAggregateCountsFromAllPasses()
    {
        var pipeline = CreatePipeline();
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        var stage = new ClassificationAndPromotionStage(
        [
            new CountingPass(facts: 2, relations: 1, candidates: 0, unresolved: 0),
            new CountingPass(facts: 3, relations: 4, candidates: 0, unresolved: 0),
        ]);

        var result = await stage.ExecuteAsync(pipeline, CancellationToken.None);

        Assert.Equal(5, result.FactCount);
        Assert.Equal(0, result.ObservationCount);
        Assert.Equal(5, result.RelationCount);
        Assert.False(result.HasUnknownsOrCandidatesOrFrontiers);
    }

    [Fact]
    [Trait("Requirement", "EBC-30")]
    public async Task ExecuteAsync_SetsHasUnknownsWhenCandidatesExist()
    {
        var pipeline = CreatePipeline();
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        var stage = new ClassificationAndPromotionStage([new CandidatePass()]);

        var result = await stage.ExecuteAsync(pipeline, CancellationToken.None);

        Assert.True(result.HasUnknownsOrCandidatesOrFrontiers);
        Assert.Equal(CreateCandidate(), Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates.ToArray()));
        Assert.Equal(0, result.RelationCount);
    }

    [Fact]
    [Trait("Requirement", "EBC-30")]
    public async Task ExecuteAsync_SetsHasUnknownsWhenUnresolvedExist()
    {
        var pipeline = CreatePipeline();
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        var stage = new ClassificationAndPromotionStage([new UnresolvedPass()]);

        var result = await stage.ExecuteAsync(pipeline, CancellationToken.None);

        Assert.True(result.HasUnknownsOrCandidatesOrFrontiers);
        Assert.Equal(CreateUnresolved(), Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "EBC-31")]
    public async Task ExecuteAsync_SkipFromOnePass_DoesNotAbortLaterPasses()
    {
        var pipeline = CreatePipeline();
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        var order = new List<string>();
        var stage = new ClassificationAndPromotionStage(
        [
            new RecordingPass("skip", order),
            new ProjectAddingPass(),
            new RecordingPass("after-skip", order),
        ]);

        var result = await stage.ExecuteAsync(pipeline, CancellationToken.None);

        Assert.Equal(["skip", "after-skip"], order);
        Assert.Equal(1, result.FactCount);
        Assert.Equal(Project.Create(OrdersProject), Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Project>().ToArray()));
        Assert.False(result.StructuralCorruption);
    }

    private static PipelineContext CreatePipeline() =>
        new(new SwallowingSession(), "alpha.sln");

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static CandidateLink CreateCandidate() =>
        CandidateLink.Create(
            RelationKind.Targets,
            Solution.Create(AcmeSolution).Reference,
            Project.Create(OrdersProject).Reference,
            EvidenceChain.Create([CreateObservationIdentity()]));

    private static UnresolvedRecord CreateUnresolved() =>
        UnresolvedRecord.Create(
            RelationKind.Targets,
            Solution.Create(AcmeSolution).Reference,
            UnresolvedCause.InsufficientEvidence,
            EvidenceChain.Create([CreateObservationIdentity()]));

    private static ObservationIdentity CreateObservationIdentity() =>
        new(Solution.Create(AcmeSolution).Reference, ObservationKind.Invocation, NormalizedPayload.Create([]), 1);

    private sealed class RecordingPass(string name, List<string> order) : IClassifierPass
    {
        public string Name => name;

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            order.Add(Name);
            return new ClassifierPassResult(0, 0, 0, 0);
        }
    }

    private sealed class ProjectAddingPass : IClassifierPass
    {
        public string Name => "add-project";

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            context.Accumulator.AddFact(Project.Create(OrdersProject));
            return new ClassifierPassResult(1, 0, 0, 0);
        }
    }

    private sealed class ProjectCountingPass(List<int> seen) : IClassifierPass
    {
        public string Name => "count-projects";

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            seen.Add(context.FactsByType<Project>().Length);
            return new ClassifierPassResult(0, 0, 0, 0);
        }
    }

    private sealed class CountingPass(int facts, int relations, int candidates, int unresolved) : IClassifierPass
    {
        public string Name => "counting";

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken) =>
            new(facts, relations, candidates, unresolved);
    }

    private sealed class CandidatePass : IClassifierPass
    {
        public string Name => "candidates";

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            context.Accumulator.AddCandidate(CreateCandidate());
            return new ClassifierPassResult(0, 0, 1, 0);
        }
    }

    private sealed class UnresolvedPass : IClassifierPass
    {
        public string Name => "unresolved";

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            context.Accumulator.AddUnresolved(CreateUnresolved());
            return new ClassifierPassResult(0, 0, 0, 1);
        }
    }
}
