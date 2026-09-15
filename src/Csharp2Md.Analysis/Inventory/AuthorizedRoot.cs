namespace Csharp2Md.Analysis.Inventory;

internal static class AuthorizedRoot
{
    /// <summary>
    /// The authorized root of <paramref name="solutionPath"/>, taking the existing project paths from
    /// the solution file itself. A listed project that is not on disk contributes nothing to the root.
    /// </summary>
    public static string ForSolution(string solutionPath)
    {
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))
            ?? throw new InvalidOperationException($"'{solutionPath}' has no containing directory.");
        var existing = listed
            .Select(listedPath => Path.GetFullPath(Path.Combine(solutionDirectory, listedPath)))
            .Where(File.Exists);
        return Compute(solutionPath, existing);
    }

    /// <summary>
    /// <paramref name="absolutePath"/> relative to <paramref name="root"/> with forward slashes on
    /// every platform - the logical form every fact and observation addresses a file by.
    /// </summary>
    public static string ToLogicalPath(string root, string absolutePath) =>
        Path.GetRelativePath(root, absolutePath).Replace('\\', '/');

    public static string Compute(string solutionPath, IEnumerable<string> existingProjectPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentNullException.ThrowIfNull(existingProjectPaths);

        var solutionFullPath = Path.GetFullPath(solutionPath);
        var directories = new List<string> { ContainingDirectory(solutionFullPath) };
        foreach (var projectPath in existingProjectPaths)
        {
            var candidate = Path.GetFullPath(projectPath);
            if (!File.Exists(candidate))
            {
                continue;
            }

            directories.Add(ContainingDirectory(candidate));
        }

        return SmallestContainingDirectory(directories);
    }

    private static string SmallestContainingDirectory(IReadOnlyList<string> directories)
    {
        var root = Normalize(directories[0]);
        for (var index = 1; index < directories.Count; index++)
        {
            var directory = Normalize(directories[index]);
            while (!ContainsPath(root, directory))
            {
                var parent = Directory.GetParent(root)
                    ?? throw new InvalidOperationException($"No common ancestor for '{root}' and '{directory}'.");
                root = Normalize(parent.FullName);
            }
        }

        return root;
    }

    private static bool ContainsPath(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (path.Equals(root, comparison))
        {
            return true;
        }

        var prefix = root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, comparison);
    }

    private static string ContainingDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        return string.IsNullOrEmpty(directory)
            ? throw new ArgumentException($"'{filePath}' has no containing directory.", nameof(filePath))
            : Normalize(directory);
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
