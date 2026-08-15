using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Loading;

/// <summary>
/// Distinguishes "possibly missing restore" from a generic compile error. <c>GetCompilationAsync()</c>
/// does not return null for this case (Project.SupportsCompilation is true) — a project with
/// unrestored packages still produces a real Compilation, just one whose diagnostics report
/// unresolved type/namespace references (dotnet/roslyn#52293).
/// </summary>
public static class RestoreHeuristics
{
    // CS0246: type or namespace not found. CS0234: type or namespace doesn't exist in namespace.
    // CS0012: type defined in an assembly that isn't referenced (a resolvable-but-unrestored package).
    private static readonly IReadOnlySet<string> MissingRestoreDiagnosticIds =
        new HashSet<string>(["CS0246", "CS0234", "CS0012"]);

    public static bool LooksLikeMissingRestore(Diagnostic diagnostic) =>
        diagnostic.Severity == DiagnosticSeverity.Error
        && MissingRestoreDiagnosticIds.Contains(diagnostic.Id);
}
