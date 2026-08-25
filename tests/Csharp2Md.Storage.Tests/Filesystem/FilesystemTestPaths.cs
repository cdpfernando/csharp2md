using System.Security.Cryptography;
using System.Text;

namespace Csharp2Md.Storage.Tests.Filesystem;

internal static class FilesystemTestPaths
{
    public static string ChildName(string solutionKey)
    {
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(solutionKey)))[..32];
        return "s-" + hex;
    }

    public static string ChildDirectory(string outputRoot, string solutionKey) =>
        Path.Combine(outputRoot, ChildName(solutionKey));

    public static string SolutionHex(string solutionKey) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(solutionKey)))[..32];

    public static IReadOnlyDictionary<string, byte[]> SnapshotFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
    }
}
