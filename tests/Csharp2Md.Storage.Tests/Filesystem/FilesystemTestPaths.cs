using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Storage.Tests.Filesystem;

internal static class FilesystemTestPaths
{
    public static string ChildName(string solutionKey)
    {
        var hex = SolutionHex(solutionKey);
        return "s-" + hex;
    }

    public static string ChildDirectory(string outputRoot, string solutionKey) =>
        Path.Combine(outputRoot, ChildName(solutionKey));

    public static string SolutionHex(string solutionKey)
    {
        var identity = SolutionCoordinate.For(solutionKey).Identity.Value;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
    }

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
