namespace Csharp2Md.Projection.Tests;

internal static class ProjectionTestPaths
{
    public static readonly string RepoRoot = FindRepoRoot();

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
