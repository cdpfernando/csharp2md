namespace Csharp2Md.Analysis;

public interface IAnalysisEngine
{
    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken);
}
