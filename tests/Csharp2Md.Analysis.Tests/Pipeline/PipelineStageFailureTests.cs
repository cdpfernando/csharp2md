using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class PipelineStageFailureTests
{
    [Fact]
    [Trait("Requirement", "ENG-19")]
    [Trait("Requirement", "ENG-25")]
    public async Task AnalyzeAsync_ThrowingSubstitute_SkipsLaterStagesAndDoesNotPublish()
    {
        var executed = new List<string>();
        var thrower = new ThrowingStage("Classification and Promotion");
        var stages = StubStages.DeclaredNames
            .Select((name, index) => index == 3
                ? (IPipelineStage)thrower
                : new RecordingStage(
                    name,
                    executed,
                    onExecute: index == 0
                        ? static context => context.Session.Stage(FactualSnapshot.Empty)
                        : null))
            .ToImmutableArray();
        var store = new InMemoryTransactionalStore();
        var engine = new AnalysisEngine(store, stages);
        var solutionPath = "alpha.sln";

        var result = await engine.AnalyzeAsync(AnalysisRequest.Create([solutionPath]), CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.Equal(thrower.Name, outcome.FailingStage);
        Assert.Equal("InvalidOperationException: Stage 'Classification and Promotion' failed.", outcome.Detail);
        Assert.True(result.HasUnpublishedSolution);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            executed);
        Assert.DoesNotContain("Validation and Coverage", executed);
        Assert.DoesNotContain("Persistence", executed);
        Assert.Equal(
            ["Inventory", "Semantic Analysis", "Observation Extraction"],
            outcome.Stages.Select(report => report.Name).ToArray());
        Assert.False(store.TryGetPublication(Path.GetFullPath(solutionPath), out _));
    }

    [Fact]
    public async Task AnalyzeAsync_NestedStageFailure_ReportsOnlyTheSanitizedRootCause()
    {
        const string secret = "pipeline-secret";
        const string environmentValue = "private-environment-value";
        var sourcePath = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "private", "OrderHandler.cs");
        var root = new InvalidOperationException(
            $"Could not parse {sourcePath}; Password={secret}{Environment.NewLine}source: return customer.Password;");
        var failure = new ApplicationException("outer pipeline wrapper", root);
        failure.Data["environment"] = environmentValue;
        var stage = new ThrowingStage("Semantic Analysis", failure);
        var engine = new AnalysisEngine(
            new InMemoryTransactionalStore(),
            StubStages.CreateDefault().SetItem(1, stage));

        var result = await engine.AnalyzeAsync(
            AnalysisRequest.Create(["alpha.sln"]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
        Assert.Equal(stage.Name, outcome.FailingStage);
        var detail = Assert.IsType<string>(outcome.Detail);
        Assert.Equal("InvalidOperationException: Could not parse", detail);
        Assert.DoesNotContain(sourcePath, detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, detail, StringComparison.Ordinal);
        Assert.DoesNotContain("return customer.Password", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("outer pipeline wrapper", detail, StringComparison.Ordinal);
        Assert.DoesNotContain(environmentValue, detail, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', detail);
        Assert.DoesNotContain('\n', detail);
    }

    [Fact]
    public async Task AnalyzeAsync_FailedRetry_AbortsStagingAndPreservesCommittedBytes()
    {
        const string solutionPath = "alpha.sln";
        var canonicalPath = Path.GetFullPath(solutionPath);
        var store = new InMemoryTransactionalStore();
        var request = AnalysisRequest.Create([solutionPath]);
        var successfulEngine = new AnalysisEngine(store, StubStages.CreateDefault());
        var first = await successfulEngine.AnalyzeAsync(request, CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(first.Solutions).Status);
        Assert.True(store.TryGetPublication(canonicalPath, out var prior));

        var failedEngine = new AnalysisEngine(
            store,
            StubStages.CreateDefault().SetItem(3, new ThrowingStage("Classification and Promotion")));
        var failed = await failedEngine.AnalyzeAsync(request, CancellationToken.None);

        Assert.Equal(PublicationStatus.Unpublished, Assert.Single(failed.Solutions).Status);
        Assert.True(store.TryGetPublication(canonicalPath, out var kept));
        Assert.Equal(prior.SolutionKey, kept.SolutionKey);
        Assert.Equal(prior.ArtifactsInPublicationOrder.Length, kept.ArtifactsInPublicationOrder.Length);
        for (var index = 0; index < prior.ArtifactsInPublicationOrder.Length; index++)
        {
            Assert.Equal(prior.ArtifactsInPublicationOrder[index].Role, kept.ArtifactsInPublicationOrder[index].Role);
            Assert.Equal(prior.ArtifactsInPublicationOrder[index].CanonicalKey, kept.ArtifactsInPublicationOrder[index].CanonicalKey);
            Assert.True(
                prior.ArtifactsInPublicationOrder[index].Payload.AsSpan()
                    .SequenceEqual(kept.ArtifactsInPublicationOrder[index].Payload.AsSpan()),
                $"Payload bytes at '{prior.ArtifactsInPublicationOrder[index].CanonicalKey}' changed after the failed retry.");
        }

        var retry = await successfulEngine.AnalyzeAsync(request, CancellationToken.None);
        Assert.Equal(PublicationStatus.Committed, Assert.Single(retry.Solutions).Status);
    }

    [Fact]
    public async Task AnalyzeAsync_FailedFilesystemRetry_RemovesStagingAndPreservesCommittedPackageBytes()
    {
        var output = Directory.CreateTempSubdirectory("csharp2md-failed-retry-");
        try
        {
            const string solutionPath = "alpha.sln";
            var request = AnalysisRequest.Create([solutionPath]);
            var store = new FilesystemTransactionalStore(output.FullName);
            var successfulEngine = new AnalysisEngine(store, StubStages.CreateDefault());

            var first = await successfulEngine.AnalyzeAsync(request, CancellationToken.None);

            Assert.Equal(PublicationStatus.Committed, Assert.Single(first.Solutions).Status);
            var package = Assert.Single(Directory.GetDirectories(output.FullName, "s-*"));
            var prior = PackageSnapshot.Capture(package);

            var failedEngine = new AnalysisEngine(
                store,
                StubStages.CreateDefault().SetItem(3, new ThrowingStage("Classification and Promotion")));
            var failed = await failedEngine.AnalyzeAsync(request, CancellationToken.None);

            Assert.Equal(PublicationStatus.Unpublished, Assert.Single(failed.Solutions).Status);
            Assert.False(Directory.Exists(package + ".staging"));
            Assert.Empty(Directory.EnumerateDirectories(output.FullName, "*.staging"));
            PackageSnapshot.AssertEqual(prior, PackageSnapshot.Capture(package));
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

}

internal sealed class ThrowingStage : IPipelineStage
{
    private readonly Exception? _exception;

    public ThrowingStage(string name, Exception? exception = null)
    {
        Name = name;
        _exception = exception;
    }

    public string Name { get; }

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        throw _exception ?? new InvalidOperationException($"Stage '{Name}' failed.");
    }
}
