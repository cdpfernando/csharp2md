using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis;

public sealed record AnalysisRequest
{
    public ImmutableArray<string> SolutionPaths { get; }

    /// <summary>
    /// Document paths, relative to a requested solution's authorized root with forward slashes,
    /// that the caller wants re-included even though the supported-document policy would otherwise
    /// exclude them (GCPC-030). Admits only the listed documents, not any other excluded class.
    /// </summary>
    public ImmutableArray<string> AllowedDocumentPaths { get; }

    private AnalysisRequest(ImmutableArray<string> solutionPaths, ImmutableArray<string> allowedDocumentPaths)
    {
        SolutionPaths = solutionPaths;
        AllowedDocumentPaths = allowedDocumentPaths;
    }

    public static AnalysisRequest Create(
        ImmutableArray<string> solutionPaths,
        ImmutableArray<string> allowedDocumentPaths = default)
    {
        if (solutionPaths.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "An analysis request requires one or more solutionPaths.",
                nameof(solutionPaths));
        }

        var seen = new HashSet<string>(CanonicalPathComparer);
        foreach (var path in solutionPaths)
        {
            var canonical = Path.GetFullPath(path);
            if (!seen.Add(canonical))
            {
                throw new ArgumentException(
                    $"The solution path '{canonical}' is specified more than once.",
                    nameof(solutionPaths));
            }
        }

        var allowlist = allowedDocumentPaths.IsDefault ? ImmutableArray<string>.Empty : allowedDocumentPaths;
        ValidateAllowlist(solutionPaths, allowlist);

        return new AnalysisRequest(solutionPaths, allowlist);
    }

    private static void ValidateAllowlist(ImmutableArray<string> solutionPaths, ImmutableArray<string> allowlist)
    {
        if (allowlist.IsEmpty)
        {
            return;
        }

        var roots = solutionPaths.Select(ComputeAuthorizedRoot).ToArray();
        foreach (var entry in allowlist)
        {
            if (!roots.Any(root => IsWithinRoot(root, entry)))
            {
                throw new ArgumentException(
                    $"The allowlist entry '{entry}' does not resolve inside any requested solution's authorized root.",
                    "allowedDocumentPaths");
            }
        }
    }

    private static string ComputeAuthorizedRoot(string solutionPath)
    {
        var fullSolutionPath = Path.GetFullPath(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(fullSolutionPath)
            ?? throw new ArgumentException($"'{solutionPath}' has no containing directory.", nameof(solutionPath));
        var listed = SolutionFileReader.ReadProjectPaths(fullSolutionPath);
        var existing = listed
            .Select(relative => Path.GetFullPath(Path.Combine(solutionDirectory, relative)))
            .Where(File.Exists)
            .ToArray();
        return AuthorizedRoot.Compute(fullSolutionPath, existing);
    }

    private static bool IsWithinRoot(string root, string relativeEntry)
    {
        var absolute = Path.GetFullPath(Path.Combine(root, relativeEntry.Replace('/', Path.DirectorySeparatorChar)));
        try
        {
            PathGuard.RejectEscapes(root, absolute);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static StringComparer CanonicalPathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
