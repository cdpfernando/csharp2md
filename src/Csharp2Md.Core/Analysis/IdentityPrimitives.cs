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

    // A symbol's display string alone is not solution-unique: Roslyn renders some compiler-synthesized
    // symbols (a top-level-statements program's entry point, for one) with the exact same text in every
    // project, and two unrelated symbols could otherwise collide onto one entity. Qualifying by the
    // symbol's own owning project keeps genuinely distinct per-project symbols distinct while a symbol
    // that really is declared once and shared (its owner resolves the same way regardless of caller)
    // still unifies to one key, as DEP-01/VAR-03 require.
    public static string CreateEntityKey(SolutionIdentity solution, EntityKind kind, ProjectIdentity owner, string logicalName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return CreateEntityKey(solution, kind, owner.LogicalRelativePath + ":" + logicalName);
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
