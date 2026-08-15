using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Csharp2Md.Core.Loading;

/// <summary>
/// Owns the <see cref="MSBuildWorkspace"/> lifecycle for one service. Never call
/// <c>OpenProjectAsync</c> in a loop (P1-05) — every service, regardless of how its boundary was
/// resolved, is expected to arrive here as a single solution path.
/// </summary>
public sealed class SolutionLoader
{
    public async Task<LoadedService> LoadAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        using var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;

        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);

        var results = new List<ProjectLoadResult>();
        foreach (var project in solution.Projects)
        {
            results.Add(await ClassifyAsync(project, workspace.Diagnostics, cancellationToken));
        }

        return new LoadedService(solution, new LoadReport(results));
    }

    /// <summary>
    /// Classifies a single project's load health. Split out from <see cref="LoadAsync"/> so it can
    /// be exercised against a project built with any <see cref="Microsoft.CodeAnalysis.Workspace"/>
    /// (not just <see cref="MSBuildWorkspace"/>) — in particular, testing the
    /// <see cref="ProjectLoadStatus.UnsupportedForCompilation"/> branch needs a project whose
    /// language has no registered <c>ICompilationFactoryService</c>, which an
    /// <see cref="Microsoft.CodeAnalysis.AdhocWorkspace"/> can construct without MSBuild at all.
    /// </summary>
    internal static async Task<ProjectLoadResult> ClassifyAsync(
        Project project, IReadOnlyList<WorkspaceDiagnostic> workspaceDiagnostics, CancellationToken cancellationToken)
    {
        // Kind == Failure is a global list, not per-project (WorkspaceDiagnostic carries no
        // ProjectId) — best-effort correlation is by file-path containment in the message text.
        var correlatedFailures = workspaceDiagnostics
            .Where(d => d.Kind == WorkspaceDiagnosticKind.Failure
                        && project.FilePath is not null
                        && d.Message.Contains(project.FilePath, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Message)
            .ToList();

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            // Project.SupportsCompilation == false — not a failure, a project type Roslyn doesn't
            // model as a Compilation at all (P1-09).
            return new ProjectLoadResult(project.Name, ProjectLoadStatus.UnsupportedForCompilation, []);
        }

        var missingRestoreMessages = compilation.GetDiagnostics(cancellationToken)
            .Where(RestoreHeuristics.LooksLikeMissingRestore)
            .Select(d => d.ToString())
            .ToList();

        if (missingRestoreMessages.Count > 0)
        {
            // Corroborated with compilation diagnostics rather than trusting Kind == Failure alone
            // (design.md Risk: NuGet warnings are sometimes misclassified as Failure upstream).
            return new ProjectLoadResult(
                project.Name, ProjectLoadStatus.PossibleMissingRestore, missingRestoreMessages);
        }

        if (correlatedFailures.Count > 0)
        {
            return new ProjectLoadResult(project.Name, ProjectLoadStatus.Degraded, correlatedFailures);
        }

        return new ProjectLoadResult(project.Name, ProjectLoadStatus.Ok, []);
    }
}
