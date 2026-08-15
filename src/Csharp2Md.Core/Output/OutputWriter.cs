using Csharp2Md.Core.Rendering;

namespace Csharp2Md.Core.Output;

/// <summary>
/// Writes rendered documents to disk, mirroring each document's path within its service source
/// tree (P1-11), and clears prior output so a run always reflects current source (P1-15).
/// </summary>
/// <param name="outputRoot">
/// Root the mirrored tree is written beneath. <see cref="PrepareRun"/> deletes everything under
/// it, so it must be the run's own output directory, called once before any service is processed.
/// </param>
public sealed class OutputWriter(string outputRoot)
{
    private static readonly string[] ExcludedDirectories = ["obj", "bin"];
    private static readonly string[] ExcludedFileSuffixes = [".g.cs", ".designer.cs"];

    public string OutputRoot { get; } = outputRoot;

    /// <summary>
    /// P1-15: deletes prior generated content wholesale. Regenerating in place would leave files
    /// behind for source that no longer exists, and stale documentation is worse than none.
    /// </summary>
    public void PrepareRun()
    {
        if (Directory.Exists(OutputRoot))
        {
            Directory.Delete(OutputRoot, recursive: true);
        }

        Directory.CreateDirectory(OutputRoot);
    }

    /// <summary>
    /// Writes one document and returns the path written, or <c>null</c> when the document is
    /// excluded from generation and nothing was written.
    /// </summary>
    public string? Write(RenderedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (IsExcluded(document.RelativePath))
        {
            return null;
        }

        var path = OutputPath(OutputRoot, document.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, document.ToMarkdown());

        return path;
    }

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

    /// <summary>Mirrors the source path and appends <c>.md</c>, so <c>Foo.cs</c> becomes <c>Foo.cs.md</c>.</summary>
    public static string OutputPath(string outputRoot, string relativePath) =>
        Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)) + ".md";
}
