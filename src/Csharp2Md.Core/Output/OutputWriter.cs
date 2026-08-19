namespace Csharp2Md.Core.Output;

/// <summary>
/// Writes rendered documents to disk, mirroring each document's path within its service source
/// tree (P1-11), and clears prior output so a run always reflects current source (P1-15).
/// </summary>
/// <param name="outputRoot">
/// Root the mirrored tree is written beneath. <see cref="PrepareRun"/> replaces prior generated
/// content only after validating that this is a safe output directory.
/// </param>
public sealed class OutputWriter(string outputRoot)
{
    private const string OwnershipMarkerName = ".csharp2md-output";
    private static readonly string[] ExcludedDirectories = ["obj", "bin"];
    private static readonly string[] ExcludedFileSuffixes = [".g.cs", ".designer.cs"];

    public string OutputRoot { get; } = outputRoot;

    /// <summary>
    /// Replaces prior generated content after enforcing the output ownership and path-safety
    /// contract. Regenerating in place would leave stale documentation behind.
    /// </summary>
    public void PrepareRun(string inputRoot, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(inputRoot);

        if (ValidateSafety(OutputRoot, inputRoot) is { } safetyError)
        {
            throw new OutputPreparationException(safetyError);
        }

        var markerPath = Path.Combine(OutputRoot, OwnershipMarkerName);
        if (Directory.Exists(OutputRoot))
        {
            var hasContent = Directory.EnumerateFileSystemEntries(OutputRoot).Any();
            if (hasContent && !File.Exists(markerPath) && !force)
            {
                throw new OutputPreparationException(
                    $"Output directory is not empty and was not created by csharp2md: {OutputRoot}. "
                    + "Choose another directory or use --force to replace its contents.");
            }

            Directory.Delete(OutputRoot, recursive: true);
        }

        Directory.CreateDirectory(OutputRoot);
        File.WriteAllText(markerPath, "csharp2md generated output\n");
    }

    internal static string? ValidateSafety(string outputRoot, string inputRoot)
    {
        var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputRoot));
        var input = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inputRoot));
        var filesystemRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(output)!);

        if (PathEquals(output, filesystemRoot))
        {
            return $"Output directory cannot be a filesystem root: {output}.";
        }

        if (IsSameOrAncestor(output, input))
        {
            return $"Output directory cannot equal or contain the input directory: {output}.";
        }

        return null;
    }

    private static bool IsSameOrAncestor(string candidate, string path)
    {
        var relative = Path.GetRelativePath(candidate, path);
        return relative == "."
            || (!Path.IsPathRooted(relative)
                && relative != ".."
                && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>Build-output and generated files are noise, not documentation (spec Assumptions).</summary>
    public static bool IsExcluded(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (ExcludedDirectories.Contains(segments[i], StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var fileName = segments.Length > 0 ? segments[^1] : relativePath;

        return ExcludedFileSuffixes.Any(suffix => fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

}

public sealed class OutputPreparationException(string message) : InvalidOperationException(message);
