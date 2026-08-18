using System.Collections.Frozen;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Indexes;

internal sealed record TargetAnalysisIndexInput(
    TargetFact Target,
    ImmutableArray<SymbolFact> Symbols,
    ImmutableArray<RelationFact> Relations);

internal sealed record ProjectReferenceIndexEntry(
    string Reference,
    ProjectFactId? TargetProjectId);

internal sealed class SolutionAnalysisIndex
{
    private readonly ImmutableArray<ProjectFact> _orderedProjects;
    private readonly FrozenDictionary<ProjectFactId, ProjectFact> _projects;
    private readonly FrozenDictionary<ProjectFactId, ImmutableArray<TargetFactId>> _targetIdsByProject;
    private readonly FrozenDictionary<TargetFactId, TargetSummary> _targets;

    private SolutionAnalysisIndex(
        ImmutableArray<ProjectFact> orderedProjects,
        FrozenDictionary<ProjectFactId, ProjectFact> projects,
        FrozenDictionary<ProjectFactId, ImmutableArray<TargetFactId>> targetIdsByProject,
        FrozenDictionary<TargetFactId, TargetSummary> targets)
    {
        _orderedProjects = orderedProjects;
        _projects = projects;
        _targetIdsByProject = targetIdsByProject;
        _targets = targets;
    }

    public ImmutableArray<ProjectFact> Projects => _orderedProjects;

    public static SolutionAnalysisIndex Build(
        IEnumerable<ProjectFact> projects,
        IEnumerable<TargetAnalysisIndexInput> targets)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(targets);

        var orderedProjects = projects
            .OrderBy(static project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var projectsById = ToUniqueFrozenDictionary(
            orderedProjects,
            static project => project.ProjectId,
            "Duplicate project identities cannot be indexed.");
        var projectsByPath = orderedProjects.ToFrozenDictionary(
            static project => project.RelativePath,
            static project => project.ProjectId,
            StringComparer.Ordinal);

        var orderedTargets = targets
            .OrderBy(static input => input.Target.TargetId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var targetSummaries = new Dictionary<TargetFactId, TargetSummary>();
        foreach (var input in orderedTargets)
        {
            if (!projectsById.ContainsKey(input.Target.ProjectId))
            {
                throw new ArgumentException("Every indexed target must belong to an indexed project.", nameof(targets));
            }

            if (!targetSummaries.TryAdd(
                    input.Target.TargetId,
                    TargetSummary.Create(input, projectsById[input.Target.ProjectId], projectsByPath)))
            {
                throw new ArgumentException("Duplicate target identities cannot be indexed.", nameof(targets));
            }
        }

        var targetIdsByProject = orderedProjects.ToFrozenDictionary(
            static project => project.ProjectId,
            project => targetSummaries.Values
                .Where(summary => summary.Target.ProjectId == project.ProjectId)
                .Select(static summary => summary.Target.TargetId)
                .OrderBy(static id => id.Value, StringComparer.Ordinal)
                .ToImmutableArray());

        return new SolutionAnalysisIndex(
            orderedProjects,
            projectsById,
            targetIdsByProject,
            targetSummaries.ToFrozenDictionary());
    }

    public ProjectFact? FindProject(ProjectFactId projectId) =>
        _projects.GetValueOrDefault(projectId);

    public ImmutableArray<TargetFact> GetTargets(ProjectFactId projectId) =>
        _targetIdsByProject.GetValueOrDefault(projectId, [])
            .Select(targetId => _targets[targetId].Target)
            .ToImmutableArray();

    public ImmutableArray<SymbolFact> GetSymbols(TargetFactId targetId) =>
        FindTarget(targetId)?.Symbols ?? [];

    public ImmutableArray<SymbolFact> GetSymbolsReferencingType(
        TargetFactId targetId,
        string typeReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeReference);
        return FindTarget(targetId)?.SymbolsByTypeReference.GetValueOrDefault(typeReference, []) ?? [];
    }

    public ImmutableArray<ProjectReferenceIndexEntry> GetProjectReferences(TargetFactId targetId) =>
        FindTarget(targetId)?.ProjectReferences ?? [];

    public ImmutableArray<RelationFact> GetRelations(TargetFactId targetId) =>
        FindTarget(targetId)?.Relations ?? [];

    public bool HasRelation(
        ProjectFactId projectId,
        RelationPartition partition,
        string relationKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationKind);
        return _targetIdsByProject.GetValueOrDefault(projectId, [])
            .SelectMany(targetId => _targets[targetId].Relations)
            .Any(relation => relation.Partition == partition &&
                string.Equals(relation.RelationKind, relationKind, StringComparison.Ordinal));
    }

    private TargetSummary? FindTarget(TargetFactId targetId) =>
        _targets.GetValueOrDefault(targetId);

    private static FrozenDictionary<TKey, TValue> ToUniqueFrozenDictionary<TValue, TKey>(
        IEnumerable<TValue> values,
        Func<TValue, TKey> keySelector,
        string duplicateMessage)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var value in values)
        {
            if (!result.TryAdd(keySelector(value), value))
            {
                throw new ArgumentException(duplicateMessage, nameof(values));
            }
        }

        return result.ToFrozenDictionary();
    }

    private sealed record TargetSummary(
        TargetFact Target,
        ImmutableArray<SymbolFact> Symbols,
        FrozenDictionary<string, ImmutableArray<SymbolFact>> SymbolsByTypeReference,
        ImmutableArray<ProjectReferenceIndexEntry> ProjectReferences,
        ImmutableArray<RelationFact> Relations)
    {
        public static TargetSummary Create(
            TargetAnalysisIndexInput input,
            ProjectFact owner,
            FrozenDictionary<string, ProjectFactId> projectsByPath)
        {
            var symbols = input.Symbols
                .OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
                .ToImmutableArray();
            var symbolsByType = symbols
                .SelectMany(static symbol => symbol.RelevantTypeReferences.Select(type => (Type: type, Symbol: symbol)))
                .GroupBy(static item => item.Type, StringComparer.Ordinal)
                .ToFrozenDictionary(
                    static group => group.Key,
                    static group => group.Select(static item => item.Symbol).ToImmutableArray(),
                    StringComparer.Ordinal);
            var projectReferences = (input.Target.Evaluation?.ProjectReferences ?? [])
                .Select(reference => new ProjectReferenceIndexEntry(
                    reference,
                    ResolveProjectReference(owner.RelativePath, reference, projectsByPath)))
                .OrderBy(static reference => reference.Reference, StringComparer.Ordinal)
                .ToImmutableArray();
            var relations = input.Relations
                .OrderBy(static relation => relation.RelationId.Value, StringComparer.Ordinal)
                .ToImmutableArray();

            return new TargetSummary(input.Target, symbols, symbolsByType, projectReferences, relations);
        }

        private static ProjectFactId? ResolveProjectReference(
            string ownerProjectPath,
            string reference,
            FrozenDictionary<string, ProjectFactId> projectsByPath)
        {
            var normalizedReference = reference.Replace('\\', '/');
            if (projectsByPath.TryGetValue(normalizedReference, out var direct))
            {
                return direct;
            }

            if (normalizedReference.StartsWith("/", StringComparison.Ordinal) ||
                (normalizedReference.Length >= 2 && normalizedReference[1] == ':'))
            {
                return null;
            }

            var ownerSegments = ownerProjectPath.Split('/').SkipLast(1);
            var segments = new Stack<string>();
            foreach (var segment in ownerSegments.Concat(normalizedReference.Split('/')))
            {
                if (segment is "" or ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count == 0)
                    {
                        return null;
                    }

                    segments.Pop();
                    continue;
                }

                segments.Push(segment);
            }

            var normalizedPath = string.Join('/', segments.Reverse());
            return projectsByPath.TryGetValue(normalizedPath, out var projectId)
                ? projectId
                : null;
        }
    }
}
