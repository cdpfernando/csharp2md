using Csharp2Md.Core.Analysis.Inventory;
using Microsoft.CodeAnalysis.MSBuild;

namespace Csharp2Md.Core.Analysis.Semantics;

internal sealed record PlannedProjectVariant(
    string ProjectLogicalRelativePath,
    string TargetFramework);

internal sealed class VariantPlanException : InvalidOperationException
{
    public string Code { get; }

    public string Cause { get; }

    public VariantPlanException(string cause)
        : base($"variant-plan: {cause}")
    {
        Code = "variant-plan";
        Cause = cause;
    }
}

internal readonly record struct VariantResolveReport(string FilePath, string? TargetFramework);

internal static class ProjectVariantPlanner
{
    public static async Task<ImmutableArray<PlannedProjectVariant>> DiscoverAsync(
        string solutionPath,
        string authorizedRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var root = PathGuard.Normalize(authorizedRoot);
        var fullSolutionPath = Path.GetFullPath(solutionPath);
        PathGuard.RejectEscapes(root, fullSolutionPath);

        var reports = new List<VariantResolveReport>();
        var progress = new Progress<ProjectLoadProgress>(report =>
        {
            if (report.Operation != ProjectLoadOperation.Resolve)
            {
                return;
            }

            reports.Add(new VariantResolveReport(report.FilePath, report.TargetFramework));
        });

        using var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;
        try
        {
            await workspace.OpenSolutionAsync(fullSolutionPath, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not VariantPlanException)
        {
            throw new VariantPlanException($"workspace-open-failed:{exception.GetType().Name}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return BuildPlan(root, reports);
    }

    internal static ImmutableArray<PlannedProjectVariant> BuildPlan(
        string authorizedRoot,
        IEnumerable<VariantResolveReport> reports)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);
        ArgumentNullException.ThrowIfNull(reports);

        var root = PathGuard.Normalize(authorizedRoot);
        var byProject = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var seenEmpty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var report in reports)
        {
            if (string.IsNullOrWhiteSpace(report.FilePath))
            {
                throw new VariantPlanException("unmatched-tfm-progress");
            }

            if (!report.FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(report.FilePath);
            PathGuard.RejectEscapes(root, fullPath);
            var logicalPath = PathGuard.ToLogicalPath(root, fullPath);
            if (!IsSafeLogicalRelative(logicalPath))
            {
                throw new VariantPlanException($"unsafe-project-path:{logicalPath}");
            }

            if (string.IsNullOrWhiteSpace(report.TargetFramework))
            {
                seenEmpty.Add(logicalPath);
                continue;
            }

            if (!byProject.TryGetValue(logicalPath, out var frameworks))
            {
                frameworks = new HashSet<string>(StringComparer.Ordinal);
                byProject[logicalPath] = frameworks;
            }

            frameworks.Add(report.TargetFramework);
        }

        foreach (var project in seenEmpty.Order(StringComparer.Ordinal))
        {
            if (!byProject.ContainsKey(project))
            {
                throw new VariantPlanException($"missing-tfm:{project}");
            }

            throw new VariantPlanException($"ambiguous-tfm:{project}");
        }

        if (byProject.Count == 0)
        {
            throw new VariantPlanException("missing-tfm-progress");
        }

        return byProject
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .SelectMany(pair => pair.Value
                .Order(StringComparer.Ordinal)
                .Select(tfm => new PlannedProjectVariant(pair.Key, tfm)))
            .ToImmutableArray();
    }

    private static bool IsSafeLogicalRelative(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)
            || relative[0] is '/' or '\\'
            || relative.Contains('\\', StringComparison.Ordinal)
            || (relative.Length >= 2 && char.IsAsciiLetter(relative[0]) && relative[1] == ':'))
        {
            return false;
        }

        var segments = relative.Split('/');
        return !segments.Any(static segment => segment is "" or "." or "..");
    }
}
