using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis;

public sealed class AnalysisEngine : IAnalysisEngine
{
    private readonly ITransactionalStore _store;
    private readonly PipelineOrchestrator _orchestrator;

    public AnalysisEngine(ITransactionalStore store)
        : this(store, StubStages.CreateDefault())
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

        return new AnalysisResult(outcomes.ToImmutable());
    }

    private async Task<SolutionOutcome> AnalyzeSolutionAsync(string path, CancellationToken cancellationToken)
    {
        var canonical = Path.GetFullPath(path);
        var session = _store.Open(canonical);
        var context = new PipelineContext(session, path);
        await _orchestrator.RunAsync(context, cancellationToken).ConfigureAwait(false);
        session.Commit();

        return new SolutionOutcome(
            solutionPath: canonical,
            logicalRelativePath: path.Replace('\\', '/'),
            status: PublicationStatus.Committed,
            failingStage: null,
            structuralCorruption: false,
            hasUnknownsOrCandidatesOrFrontiers: false,
            stages: context.Reports);
    }
}
