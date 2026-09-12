using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class StagingOrderTests
{
    [Fact]
    [Trait("Requirement", "ENG-27")]
    [Trait("Requirement", "STOR-45")]
    public async Task AnalyzeAsync_TwoPersistenceMergeOrders_CommitIdenticalCanonicalPayloads()
    {
        var solutionPath = "alpha.sln";
        var sessionKey = Path.GetFullPath(solutionPath);
        var alpha = DocumentSnapshot("src/Acme.Payments/Alpha.cs");
        var zeta = DocumentSnapshot("src/Acme.Payments/Zeta.cs");
        var firstObservation = ObservationSnapshot(1);
        var secondObservation = ObservationSnapshot(2);

        var first = await PublishWithStagingOrder(solutionPath, zeta, alpha, secondObservation, firstObservation);
        var second = await PublishWithStagingOrder(solutionPath, alpha, zeta, firstObservation, secondObservation);

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);
        Assert.Equal(sessionKey, first.Publication.SolutionKey);
        Assert.Equal(sessionKey, second.Publication.SolutionKey);

        AssertEqualCanonicalPayloads(
            first.Publication.ArtifactsInPublicationOrder,
            second.Publication.ArtifactsInPublicationOrder);
        Assert.Contains(
            first.Publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey == "facts/structural.json");
        Assert.Contains(
            first.Publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal));
        Assert.Equal(ArtifactRole.Manifest, first.Publication.ArtifactsInPublicationOrder[^1].Role);
        Assert.Equal(ArtifactRole.Manifest, second.Publication.ArtifactsInPublicationOrder[^1].Role);
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> PublishWithStagingOrder(
        string solutionPath,
        params FactualSnapshot[] snapshots)
    {
        var store = new InMemoryTransactionalStore();
        var persistence = new StagingPersistence(snapshots);
        var engine = new AnalysisEngine(store, StubStages.CreateDefault().SetItem(5, persistence));

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static void AssertEqualCanonicalPayloads(
        ImmutableArray<StagedFragment> left,
        ImmutableArray<StagedFragment> right)
    {
        var leftCanonical = WithoutMeasurements(left);
        var rightCanonical = WithoutMeasurements(right);
        Assert.Equal(leftCanonical.Length, rightCanonical.Length);
        for (var index = 0; index < leftCanonical.Length; index++)
        {
            Assert.Equal(leftCanonical[index].Role, rightCanonical[index].Role);
            Assert.Equal(leftCanonical[index].CanonicalKey, rightCanonical[index].CanonicalKey);
            Assert.True(
                leftCanonical[index].Payload.AsSpan().SequenceEqual(rightCanonical[index].Payload.AsSpan()),
                $"Canonical payload bytes at '{leftCanonical[index].CanonicalKey}' differ.");
        }
    }

    private static ImmutableArray<StagedFragment> WithoutMeasurements(ImmutableArray<StagedFragment> artifacts) =>
        [.. artifacts.Where(fragment => fragment.CanonicalKey != "measurements.json")];

    private static FactualSnapshot DocumentSnapshot(string relativePath)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = SolutionId.Create(workspace, "src/Acme.sln");
        var project = ProjectId.Create(solution, "src/Acme.Payments/Acme.Payments.csproj");
        return new FactualSnapshot([Document.Create(project, relativePath)], [], [], [], [], []);
    }

    private static FactualSnapshot ObservationSnapshot(int ordinal)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));
        var observation = Observation.Create(
            solution.Reference,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
        return new FactualSnapshot([], [observation], [], [], [], []);
    }
}

internal sealed class StagingPersistence : IPipelineStage
{
    private readonly ImmutableArray<FactualSnapshot> _snapshots;

    public StagingPersistence(params FactualSnapshot[] snapshots) => _snapshots = [.. snapshots];

    public string Name => "Persistence";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        foreach (var snapshot in _snapshots)
        {
            context.Session.Stage(snapshot);
        }

        return ValueTask.FromResult(StageResult.Zero);
    }
}
