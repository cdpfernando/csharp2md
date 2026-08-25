using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Pipeline;

internal sealed class PipelineContext
{
    public IStoreSession Session { get; }

    public string SolutionPath { get; }

    public PipelineContext(IStoreSession session, string solutionPath)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(solutionPath);
        Session = session;
        SolutionPath = solutionPath;
    }
}
