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

        var outcomes = ImmutableArray.CreateBuilder<SolutionOutcome>(request.SolutionPaths.Length);
        foreach (var path in request.SolutionPaths)
        {
            outcomes.Add(await AnalyzeSolutionAsync(path, cancellationToken).ConfigureAwait(false));
        }

        outcomes.Sort(static (left, right) =>
            string.Compare(left.LogicalRelativePath, right.LogicalRelativePath, StringComparison.Ordinal));

        return new AnalysisResult(outcomes.ToImmutable());
    }

    private async Task<SolutionOutcome> AnalyzeSolutionAsync(string path, CancellationToken cancellationToken)
    {
        var canonical = Path.GetFullPath(path);
        var session = _store.Open(canonical);
        var context = new PipelineContext(session, path);
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
        catch (PublicationRejectedException)
        {
            session.Abort();
            return CreateOutcome(
                canonical,
                path,
                PublicationStatus.Unpublished,
                failingStage: null,
                context.Reports,
                run with { StructuralCorruption = true },
                context.Detail);
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
}
