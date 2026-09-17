namespace Csharp2Md.Core.Analysis.Inventory;

internal static class PathGuard
{
    public static void RejectEscapes(string root, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedRoot = Normalize(root);
        var fullPath = Path.GetFullPath(path);
        var info = CreateInfo(fullPath);
        var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
        var effectivePath = resolved is null ? info.FullName : resolved.FullName;

        if (ContainsPath(normalizedRoot, Normalize(effectivePath)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"The inventory path '{fullPath}' escapes the authorized root '{normalizedRoot}'.");
    }

    public static bool ContainsPath(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalizedRoot = Normalize(root);
        var normalizedCandidate = Normalize(candidate);
        if (normalizedCandidate.Equals(normalizedRoot, comparison))
        {
            return true;
        }

        return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison);
    }

    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string ToLogicalPath(string root, string absolutePath) =>
        Path.GetRelativePath(root, absolutePath).Replace('\\', '/');

    private static FileSystemInfo CreateInfo(string fullPath)
    {
        FileSystemInfo file = new FileInfo(fullPath);
        if (file.Exists || file.LinkTarget is not null)
        {
            return file;
        }

        return new DirectoryInfo(fullPath);
    }
}
