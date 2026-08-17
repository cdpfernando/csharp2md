namespace Csharp2Md.Core.Analysis.Contracts;

/// <summary>The sole caller-visible outcome of an analysis run.</summary>
public sealed record AnalysisResult(
    int ExitCode,
    AnalysisMode RequestedMode,
    AnalysisMode EffectiveMode,
    string Summary,
    ImmutableArray<string> Diagnostics)
{
    public static AnalysisResult InvalidRequest(AnalysisRequestResult validation) => new(
        ExitCode: 1,
        RequestedMode: AnalysisMode.SyntaxOnly,
        EffectiveMode: AnalysisMode.SyntaxOnly,
        Summary: validation.Message ?? "Invalid analysis request.",
        Diagnostics: validation.Message is { } message ? [message] : []);
}
