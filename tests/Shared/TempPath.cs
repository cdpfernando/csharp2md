using System.Diagnostics;

namespace Csharp2Md.Tests.Shared;

/// <summary>
/// Temp-path helpers shared by every test project through <c>tests/Directory.Build.props</c>.
/// </summary>
internal static class TempPath
{
    /// <summary>Builds a unique path under the system temp directory without creating anything.</summary>
    internal static string Unique(string prefix) =>
        Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Deletes <paramref name="path"/>, whether it names a file or a directory tree. A temp path the
    /// filesystem still holds is logged rather than thrown, so cleanup never turns a passing test red.
    /// </summary>
    internal static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException exception)
        {
            Debug.WriteLine($"Failed to delete '{path}': {exception.Message}");
        }
    }
}

/// <summary>A temp directory, created or merely named, that deletes itself on dispose.</summary>
internal sealed class TempOutputRoot : IDisposable
{
    private const string DefaultPrefix = "csharp2md-fs-";

    public string DirectoryPath { get; }

    public static TempOutputRoot Create(string prefix = DefaultPrefix) => new(prefix, create: true);

    public static TempOutputRoot Uncreated(string prefix = DefaultPrefix) => new(prefix, create: false);

    private TempOutputRoot(string prefix, bool create)
    {
        DirectoryPath = TempPath.Unique(prefix);
        if (create)
        {
            Directory.CreateDirectory(DirectoryPath);
        }
    }

    public void Dispose() => TempPath.TryDelete(DirectoryPath);
}
