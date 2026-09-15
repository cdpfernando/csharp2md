using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

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

    public ImmutableArray<Csharp2Md.Domain.Facts.Document> CSharpDocuments { get; set; } = [];

    public string AuthorizedRoot { get; set; } = "";

    public ImmutableArray<Csharp2Md.Domain.Facts.Document> ConfigurationDocuments { get; set; } = [];

    public ImmutableArray<string> AllowedDocumentPaths { get; set; } = [];

    public BoundSolution? BoundSolution { get; set; }

    public ImmutableArray<AnalysisVariantId> AnalysisVariants { get; set; } = [];

    public ISourceDocumentReader? SourceDocumentReader { get; set; }

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
