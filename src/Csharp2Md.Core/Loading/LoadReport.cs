namespace Csharp2Md.Core.Loading;

public enum ProjectLoadStatus
{
    Ok,
    Degraded,
    PossibleMissingRestore,
    UnsupportedForCompilation,
}

public sealed record ProjectLoadResult(
    string ProjectName,
    ProjectLoadStatus Status,
    IReadOnlyList<string> Messages);

public sealed record LoadReport(IReadOnlyList<ProjectLoadResult> Projects);
