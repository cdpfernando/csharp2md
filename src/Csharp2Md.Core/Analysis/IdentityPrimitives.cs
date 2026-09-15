namespace Csharp2Md.Core.Analysis;

internal static class CanonicalIdentity
{
    public static SolutionIdentity CreateSolution(string logicalName, string logicalRelativePath)
    {
        var name = CanonicalText.Require(logicalName, nameof(logicalName));
        var path = LogicalPath.RequireRelative(logicalRelativePath, nameof(logicalRelativePath));
        return new SolutionIdentity(Join("solution", name, path), path);
    }

    public static ProjectIdentity CreateProject(SolutionIdentity solution, string logicalRelativePath)
    {
        ArgumentNullException.ThrowIfNull(solution);
        var path = LogicalPath.RequireRelative(logicalRelativePath, nameof(logicalRelativePath));
        return new ProjectIdentity(Join("project", solution.CanonicalKey, path), path);
    }

    public static string CreateEntityKey(SolutionIdentity solution, EntityKind kind, string logicalName)
    {
        ArgumentNullException.ThrowIfNull(solution);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return Join("entity", solution.CanonicalKey, kind.ToString().ToLowerInvariant(), CanonicalText.Require(logicalName, nameof(logicalName)));
    }

    public static string CreateDocumentKey(SolutionIdentity solution, string logicalRelativePath)
    {
        ArgumentNullException.ThrowIfNull(solution);
        var path = LogicalPath.RequireRelative(logicalRelativePath, nameof(logicalRelativePath));
        return Join("document", solution.CanonicalKey, path);
    }

    public static AnalysisVariant CreateVariant(
        string targetFramework,
        string configuration,
        IEnumerable<string> symbols,
        string environment)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        var ordered = symbols
            .Select(symbol => CanonicalText.Require(symbol, nameof(symbols)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        return new AnalysisVariant(targetFramework, configuration, ordered, environment);
    }

    public static string VariantKey(AnalysisVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        var symbols = variant.Symbols.IsDefaultOrEmpty ? "-" : string.Join(',', variant.Symbols);
        return Join("variant", variant.TargetFramework, variant.Configuration, symbols, variant.Environment);
    }

    public static LogicalLocator CreateLocator(string relativePath, SourceSpan span, ProjectIdentity project) =>
        new(relativePath, span, project);

    private static string Join(string kind, params string[] parts) =>
        kind + ":" + string.Join(':', parts);
}
