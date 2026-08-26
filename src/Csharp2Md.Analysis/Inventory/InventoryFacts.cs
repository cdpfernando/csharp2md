using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Inventory;

internal sealed record InventoryFactSet(Solution Solution, ImmutableArray<Project> Projects);

internal static class InventoryFacts
{
    public static InventoryFactSet Create(
        string solutionPath,
        IEnumerable<string> listedProjectPaths,
        string authorizedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentNullException.ThrowIfNull(listedProjectPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedRoot);

        var solution = Solution.Create(
            SolutionId.Create(WorkspaceIdentity.Create("default"), Path.GetFileName(solutionPath)));
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))
            ?? throw new ArgumentException($"'{solutionPath}' has no containing directory.", nameof(solutionPath));
        var root = Path.GetFullPath(authorizedRoot);

        var projects = ImmutableArray.CreateBuilder<Project>();
        foreach (var listed in listedProjectPaths)
        {
            var absolute = Path.GetFullPath(Path.Combine(solutionDirectory, listed));
            if (!File.Exists(absolute))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, absolute).Replace('\\', '/');
            projects.Add(Project.Create(ProjectId.Create(solution.Id, relative)));
        }

        return new InventoryFactSet(solution, projects.ToImmutable());
    }
}
