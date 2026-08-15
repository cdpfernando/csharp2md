using System.Text;
using Csharp2Md.Core.Loading;

namespace Csharp2Md.Core.Pipeline;

/// <summary>
/// Builds the end-of-run summary: every project recorded as degraded or possibly missing a restore,
/// each with a <c>dotnet restore</c> suggestion (P1-10).
/// </summary>
/// <remarks>
/// A project classified <see cref="ProjectLoadStatus.UnsupportedForCompilation"/> is deliberately
/// absent from the summary. P1-09 says such a project is not a failure — it is a project type Roslyn
/// does not model as a compilation at all, and listing it would send the reader to run
/// <c>dotnet restore</c> against something a restore cannot fix.
/// </remarks>
public static class RunReporter
{
    public static string Summarize(LoadReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var affected = report.Projects.Where(NeedsAttention).ToList();

        if (affected.Count == 0)
        {
            return $"Run summary: {report.Projects.Count} project(s) loaded, none degraded or missing a restore.\n";
        }

        var summary = new StringBuilder()
            .Append("Run summary: ")
            .Append(affected.Count)
            .Append(" of ")
            .Append(report.Projects.Count)
            .Append(" project(s) need attention.\n");

        foreach (var project in affected)
        {
            summary.Append("  - ")
                .Append(project.ProjectName)
                .Append(" (")
                .Append(Describe(project.Status))
                .Append(") — suggested fix: dotnet restore\n");
        }

        return summary.ToString();
    }

    private static bool NeedsAttention(ProjectLoadResult project) =>
        project.Status is ProjectLoadStatus.Degraded or ProjectLoadStatus.PossibleMissingRestore;

    private static string Describe(ProjectLoadStatus status) => status switch
    {
        ProjectLoadStatus.Degraded => "degraded",
        ProjectLoadStatus.PossibleMissingRestore => "possible missing restore",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Not a reportable load status."),
    };
}
