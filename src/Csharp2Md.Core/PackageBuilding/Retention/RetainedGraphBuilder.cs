using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.PackageBuilding.Retention;

internal sealed class RetentionException : InvalidOperationException
{
    public RetentionException(string cause) : base($"retention:{cause}") => Cause = cause;
    public string Cause { get; }
}

internal static class RetainedGraphBuilder
{
    private static readonly HashSet<EntityKind> RootKinds =
    [
        EntityKind.Component,
        EntityKind.DeploymentUnit,
        EntityKind.EntryPoint,
        EntityKind.BoundaryOperation,
    ];

    internal static RetainedGraph Build(FactualGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var entities = graph.Entities.ToDictionary(entity => entity.CanonicalKey, StringComparer.Ordinal);
        var evidence = graph.Evidence.ToDictionary(item => item.CanonicalKey, StringComparer.Ordinal);
        foreach (var relation in graph.Relations)
        {
            if (!entities.ContainsKey(relation.SourceCanonicalKey) ||
                !entities.ContainsKey(relation.TargetCanonicalKey) ||
                relation.EvidenceCanonicalKeys.IsDefaultOrEmpty ||
                relation.EvidenceCanonicalKeys.Any(key => !evidence.ContainsKey(key)))
            {
                throw new RetentionException("invalid-confirmed-relation");
            }
        }

        var retainedKeys = new HashSet<string>(
            graph.Entities.Where(entity => RootKinds.Contains(entity.Kind)).Select(entity => entity.CanonicalKey),
            StringComparer.Ordinal);
        var rootProjects = graph.Occurrences
            .Where(occurrence => retainedKeys.Contains(occurrence.EntityCanonicalKey))
            .Select(static occurrence => occurrence.Project.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);
        retainedKeys.UnionWith(graph.Occurrences
            .Where(occurrence => rootProjects.Contains(occurrence.Project.CanonicalKey))
            .Select(static occurrence => occurrence.EntityCanonicalKey));
        var retainedRelations = new List<FactualRelation>();
        var queue = new Queue<string>(retainedKeys.Order(StringComparer.Ordinal));
        var outgoing = graph.Relations.GroupBy(relation => relation.SourceCanonicalKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CanonicalKey, StringComparer.Ordinal));

        while (queue.TryDequeue(out var source))
        {
            if (!outgoing.TryGetValue(source, out var relations)) continue;
            foreach (var relation in relations)
            {
                retainedRelations.Add(relation);
                if (retainedKeys.Add(relation.TargetCanonicalKey)) queue.Enqueue(relation.TargetCanonicalKey);
            }
        }

        var relationKeys = retainedRelations.Select(item => item.CanonicalKey).ToHashSet(StringComparer.Ordinal);
        var orderedRelations = graph.Relations.Where(item => relationKeys.Contains(item.CanonicalKey)).OrderBy(item => item.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var evidenceKeys = orderedRelations.SelectMany(item => item.EvidenceCanonicalKeys).ToHashSet(StringComparer.Ordinal);
        var orderedEvidence = graph.Evidence.Where(item => evidenceKeys.Contains(item.CanonicalKey)).OrderBy(item => item.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var citedDocumentKeys = orderedEvidence.Select(item => item.DocumentCanonicalKey).ToHashSet(StringComparer.Ordinal);
        var retainedEntities = graph.Entities.Where(item => retainedKeys.Contains(item.CanonicalKey)).OrderBy(item => item.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var retainedOccurrences = graph.Occurrences.Where(item => retainedKeys.Contains(item.EntityCanonicalKey) || evidenceKeys.Overlaps(item.EvidenceCanonicalKeys)).ToArray();
        var retainedCount = retainedEntities.Length + orderedRelations.Length + orderedEvidence.Length + retainedOccurrences.Length;
        var sourceCount = graph.Entities.Length + graph.Relations.Length + graph.Evidence.Length + graph.Occurrences.Length;

        return new RetainedGraph(
            retainedEntities,
            orderedRelations,
            ImmutableArray<KnowledgeGap>.Empty,
            orderedEvidence,
            graph.Sources.Where(item => citedDocumentKeys.Contains(item.CanonicalKey)).OrderBy(item => item.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            new RetentionMeasurements(retainedCount, sourceCount - retainedCount));
    }
}
