using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis;

public sealed class AnalysisEngine : IAnalysisEngine
{
    private readonly ITransactionalStore _store;
    private readonly PipelineOrchestrator _orchestrator;

    public AnalysisEngine(ITransactionalStore store)
        : this(store, PipelineStages.CreateDefault())
    {
    }

    internal AnalysisEngine(ITransactionalStore store, ImmutableArray<IPipelineStage> stages)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _orchestrator = new PipelineOrchestrator(stages);
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RejectDuplicateSolutionIdentities(request.SolutionPaths);

        var outcomes = ImmutableArray.CreateBuilder<SolutionOutcome>(request.SolutionPaths.Length);
        foreach (var path in request.SolutionPaths)
        {
            outcomes.Add(await AnalyzeSolutionAsync(path, cancellationToken).ConfigureAwait(false));
        }

        outcomes.Sort(static (left, right) =>
            string.Compare(left.LogicalRelativePath, right.LogicalRelativePath, StringComparison.Ordinal));

        var published = outcomes.ToImmutable();
        try
        {
            _store.PublishBatch(ToBatchRecords(published));
            return new AnalysisResult(published);
        }
        catch (PublicationRejectedException exception)
        {
            return new AnalysisResult(published, exception.Gate, exception.Detail);
        }
    }

    private async Task<SolutionOutcome> AnalyzeSolutionAsync(string path, CancellationToken cancellationToken)
    {
        var canonical = Path.GetFullPath(path);
        var reader = new FilesystemSourceDocumentReader();
        var session = _store.Open(canonical, reader);
        var context = new PipelineContext(session, path) { SourceDocumentReader = reader };
        try
        {
            var run = await _orchestrator.RunAsync(context, cancellationToken).ConfigureAwait(false);
            if (run.Status is not PipelineCompletion.Succeeded)
            {
                session.Abort();
                return CreateOutcome(
                    canonical,
                    path,
                    PublicationStatus.Unpublished,
                    run.FailedStageName,
                    context.Reports,
                    run,
                    context.Detail);
            }

            try
            {
                session.Commit();
            }
            catch (PublicationRejectedException exception)
            {
                session.Abort();
                return CreateOutcome(
                    canonical,
                    path,
                    PublicationStatus.Unpublished,
                    failingStage: null,
                    context.Reports,
                    run with { StructuralCorruption = true },
                    exception.Message);
            }

            return CreateOutcome(
                canonical,
                path,
                PublicationStatus.Committed,
                failingStage: null,
                context.Reports,
                run,
                context.Detail);
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    private static SolutionOutcome CreateOutcome(
        string canonical,
        string path,
        PublicationStatus status,
        string? failingStage,
        ImmutableArray<StageReport> stages,
        PipelineRunResult run,
        string? detail) =>
        new(
            solutionPath: canonical,
            logicalRelativePath: path.Replace('\\', '/'),
            status,
            failingStage,
            run.StructuralCorruption,
            run.HasUnknownsOrCandidatesOrFrontiers,
            stages,
            detail);

    private static ImmutableArray<BatchSolutionRecord> ToBatchRecords(ImmutableArray<SolutionOutcome> outcomes)
    {
        var records = ImmutableArray.CreateBuilder<BatchSolutionRecord>(outcomes.Length);
        foreach (var outcome in outcomes)
        {
            var coordinate = SolutionCoordinate.For(outcome.SolutionPath);
            records.Add(new BatchSolutionRecord(
                coordinate.Identity,
                coordinate.SolutionFileName,
                outcome.Status,
                outcome.FailingStage));
        }

        return records.ToImmutable();
    }

    private static void RejectDuplicateSolutionIdentities(ImmutableArray<string> paths)
    {
        var firstPathByIdentity = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var identity = SolutionCoordinate.For(path).Identity.Value;
            if (firstPathByIdentity.TryGetValue(identity, out var firstPath))
            {
                throw new ArgumentException(
                    $"The requested solutions '{firstPath}' and '{path}' produce the same solution identity.");
            }

            firstPathByIdentity.Add(identity, path);
        }
    }
}
