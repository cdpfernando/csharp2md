namespace Csharp2Md.Analysis;

public sealed record AnalysisRequest
{
    public ImmutableArray<string> SolutionPaths { get; }

    private AnalysisRequest(ImmutableArray<string> solutionPaths)
    {
        SolutionPaths = solutionPaths;
    }

    public static AnalysisRequest Create(ImmutableArray<string> solutionPaths)
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

        return new AnalysisRequest(solutionPaths);
    }

    private static StringComparer CanonicalPathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
