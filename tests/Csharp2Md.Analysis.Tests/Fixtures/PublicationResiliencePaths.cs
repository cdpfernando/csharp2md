namespace Csharp2Md.Analysis.Tests.Fixtures;

internal static class PublicationResiliencePaths
{
    public static readonly string RootPath = Path.Combine(
        AnalysisTestPaths.RepoRoot,
        "fixtures",
        "PublicationResilience");

    public static readonly string SolutionPath = Path.Combine(RootPath, "PublicationResilience.slnx");
}
