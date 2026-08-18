using System.Collections.Frozen;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Analysis.Classification;

internal enum LibraryOwnershipKind
{
    Private,
    SharedDependency,
    Standalone,
}

internal sealed record LibraryOwnershipAssignment(
    ProjectFactId LibraryProjectId,
    LibraryOwnershipKind Kind,
    ImmutableArray<ProjectFactId> OwnerProjectIds);

internal static class LibraryOwnershipClassifier
{
    public static ImmutableArray<LibraryOwnershipAssignment> Classify(SolutionAnalysisIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        var classifications = index.Projects.ToFrozenDictionary(
            static project => project.ProjectId,
            project => ProjectClassifier.Classify(project.ProjectId, index));
        var executableRoots = classifications
            .Where(static entry => IsExecutableRoot(entry.Value))
            .Select(static entry => entry.Key)
            .OrderBy(static id => id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var reachableByRoot = executableRoots.ToFrozenDictionary(
            static root => root,
            root => ReachableProjects(root, index));

        return classifications
            .Where(static entry => entry.Value == TechnicalComponentKinds.Library)
            .Select(entry => Assignment(
                entry.Key,
                executableRoots.Where(root => reachableByRoot[root].Contains(entry.Key)).ToImmutableArray()))
            .OrderBy(static assignment => assignment.LibraryProjectId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static LibraryOwnershipAssignment Assignment(
        ProjectFactId libraryProjectId,
        ImmutableArray<ProjectFactId> executableOwners) =>
        executableOwners.Length switch
        {
            0 => new LibraryOwnershipAssignment(
                libraryProjectId,
                LibraryOwnershipKind.Standalone,
                [libraryProjectId]),
            1 => new LibraryOwnershipAssignment(
                libraryProjectId,
                LibraryOwnershipKind.Private,
                executableOwners),
            _ => new LibraryOwnershipAssignment(
                libraryProjectId,
                LibraryOwnershipKind.SharedDependency,
                executableOwners.OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray()),
        };

    private static FrozenSet<ProjectFactId> ReachableProjects(
        ProjectFactId root,
        SolutionAnalysisIndex index)
    {
        var visited = new HashSet<ProjectFactId>();
        var pending = new Stack<ProjectFactId>();
        pending.Push(root);
        while (pending.TryPop(out var current))
        {
            foreach (var reference in index.GetTargets(current)
                         .SelectMany(target => index.GetProjectReferences(target.TargetId))
                         .Select(static reference => reference.TargetProjectId)
                         .OfType<ProjectFactId>()
                         .OrderByDescending(static id => id.Value, StringComparer.Ordinal))
            {
                if (reference != root && visited.Add(reference))
                {
                    pending.Push(reference);
                }
            }
        }

        return visited.ToFrozenSet();
    }

    private static bool IsExecutableRoot(string? classification) => classification is
        TechnicalComponentKinds.WebApi or
        TechnicalComponentKinds.Worker or
        TechnicalComponentKinds.Cli;
}
