namespace Csharp2Md.Core.Tests;

internal static class TestPaths
{
    public static readonly string RepoRoot = FindRepoRoot();

    public static string SyntheticSolution(string relativePath) =>
        Path.Combine(RepoRoot, "fixtures", "SyntheticSolution", relativePath);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "csharp2md.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root (csharp2md.slnx) above " + AppContext.BaseDirectory);
    }
}
