using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Pipeline;

internal sealed class PipelineContext
{
    private readonly List<StageReport> _reports = [];

    public IStoreSession Session { get; }

    public string SolutionPath { get; }

    public ImmutableArray<StageReport> Reports => [.. _reports];

    public string? Detail { get; set; }

    public PipelineContext(IStoreSession session, string solutionPath)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(solutionPath);
        Session = session;
        SolutionPath = solutionPath;
    }

    public void Record(StageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _reports.Add(report);
    }
}
