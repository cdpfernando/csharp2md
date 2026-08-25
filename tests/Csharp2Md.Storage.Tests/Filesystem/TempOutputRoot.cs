using System.Diagnostics;

namespace Csharp2Md.Storage.Tests.Filesystem;

internal sealed class TempOutputRoot : IDisposable
{
    public string DirectoryPath { get; }

    public static TempOutputRoot Create() => new(create: true);

    public static TempOutputRoot Uncreated() => new(create: false);

    private TempOutputRoot(bool create)
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "csharp2md-fs-" + Guid.NewGuid().ToString("N"));
        if (create)
        {
            Directory.CreateDirectory(DirectoryPath);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
            else if (File.Exists(DirectoryPath))
            {
                File.Delete(DirectoryPath);
            }
        }
        catch (IOException exception)
        {
            Debug.WriteLine($"Failed to delete '{DirectoryPath}': {exception.Message}");
        }
    }
}
