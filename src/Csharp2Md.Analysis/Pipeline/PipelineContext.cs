using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;

namespace Csharp2Md.Analysis.Pipeline;

internal sealed class PipelineContext
{
    private readonly List<StageReport> _reports = [];

    public IStoreSession Session { get; }

    public string SolutionPath { get; }

    public ImmutableArray<StageReport> Reports => [.. _reports];

    public string? Detail { get; set; }

    public SnapshotAccumulator Accumulator { get; }

    public ImmutableArray<string> DeclaredTargetFrameworks { get; set; } = [];

    public ImmutableArray<Document> CSharpDocuments { get; set; } = [];

    public PipelineContext(IStoreSession session, string solutionPath)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(solutionPath);
        Session = session;
        SolutionPath = solutionPath;
        Accumulator = new SnapshotAccumulator();
    }

    public void Record(StageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _reports.Add(report);
    }
}
