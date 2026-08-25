namespace Csharp2Md.Analysis.Inventory;

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

    private static FileSystemInfo CreateInfo(string fullPath)
    {
        FileSystemInfo file = new FileInfo(fullPath);
        if (file.Exists || file.LinkTarget is not null)
        {
            return file;
        }

        return new DirectoryInfo(fullPath);
    }

    private static bool ContainsPath(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (candidate.Equals(root, comparison))
        {
            return true;
        }

        return candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
