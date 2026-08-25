using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis;

public sealed class AnalysisEngine : IAnalysisEngine
{
    public AnalysisEngine(ITransactionalStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
    }

    public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
