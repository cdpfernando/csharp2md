namespace Csharp2Md.Cli.Tests;

internal static class CliTestPaths
{
    public static readonly string RepoRoot = FindRepoRoot();

    public static string UniqueOutputPath() =>
        Path.Combine(Path.GetTempPath(), "csharp2md-cli-out-" + Guid.NewGuid().ToString("N"));

    public static void TryDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

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
