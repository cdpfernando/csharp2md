namespace Csharp2Md.Analysis.Tests.Fixtures;

internal static class CertificationCorpusPaths
{
    public static readonly string RootPath = Path.Combine(
        AnalysisTestPaths.RepoRoot,
        "fixtures",
        "CertificationCorpus");

    public static readonly string SolutionPath = Path.Combine(RootPath, "CertificationCorpus.slnx");
}
