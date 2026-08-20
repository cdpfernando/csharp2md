using System.Text;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record RelationPartitionProjection(
    RelationPartition Partition,
    ImmutableArray<RelationFactJson> Relations);

internal sealed record ComponentIndexEntry(
    string ComponentId,
    string ComponentKind,
    ImmutableArray<string> ProjectIds);

internal sealed record RelationProjectionResult(
    ImmutableArray<RelationPartitionProjection> Partitions,
    ImmutableArray<ComponentIndexEntry> Components,
    string Mermaid,
    string ComponentIndex)
{
    public RelationPartitionProjection Partition(RelationPartition partition) =>
        Partitions.Single(entry => entry.Partition == partition);
}

/// <summary>
/// Produces bounded aggregate views from already validated fragments. It retains relation summaries
/// only, and never interprets a detail such as a URL or logical service name as an identity.
/// </summary>
internal static class RelationProjector
{
    // Projected over the enum, not a hand-kept list: a new RelationPartition member cannot be silently
    // left without a projection, which is the same duplication the aggregate writer already dropped.
    private static readonly ImmutableArray<RelationPartition> Partitions =
        [.. Enum.GetValues<RelationPartition>()];

    public static RelationProjectionResult Project(IEnumerable<ValidatedFactFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        var relationBuilders = Partitions.ToDictionary(
            static partition => partition,
            static _ => ImmutableArray.CreateBuilder<RelationFactJson>());
        var components = ImmutableArray.CreateBuilder<ComponentIndexEntry>();
        var relationIds = new HashSet<string>(StringComparer.Ordinal);
        var componentIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fragment in fragments)
        {
            ArgumentNullException.ThrowIfNull(fragment);
            foreach (var fact in fragment.Facts)
            {
                switch (fact)
                {
                    case RelationFact relation:
                        if (!relationBuilders.TryGetValue(relation.Partition, out var relations))
                        {
                            throw new InvalidOperationException($"Unsupported relation partition '{relation.Partition}'.");
                        }

                        if (!relationIds.Add(relation.RelationId.Value))
                        {
                            throw new InvalidOperationException($"Duplicate relation identity '{relation.RelationId.Value}' in aggregate input.");
                        }

                        relations.Add(FactualJsonMapper.MapRelation(relation));
                        break;

                    case ComponentFact component:
                        if (!componentIds.Add(component.ComponentId.Value))
                        {
                            throw new InvalidOperationException($"Duplicate component identity '{component.ComponentId.Value}' in aggregate input.");
                        }

                        components.Add(new ComponentIndexEntry(
                            component.ComponentId.Value,
                            component.ComponentKind,
                            component.ProjectIds.Select(static project => project.Value)
                                .Distinct(StringComparer.Ordinal)
                                .Order(StringComparer.Ordinal)
                                .ToImmutableArray()));
                        break;
                }
            }
        }

        var canonicalComponents = components
            .OrderBy(static component => component.ComponentId, StringComparer.Ordinal)
            .ToImmutableArray();
        var canonicalPartitions = Partitions
            .Select(partition => new RelationPartitionProjection(
                partition,
                relationBuilders[partition]
                    .OrderBy(static relation => relation.RelationId, StringComparer.Ordinal)
                    .ToImmutableArray()))
            .ToImmutableArray();

        return new RelationProjectionResult(
            canonicalPartitions,
            canonicalComponents,
            Mermaid(canonicalPartitions, canonicalComponents),
            ComponentIndex(canonicalComponents));
    }

    private static string Mermaid(
        ImmutableArray<RelationPartitionProjection> partitions,
        ImmutableArray<ComponentIndexEntry> components)
    {
        var componentByProject = new Dictionary<string, ComponentIndexEntry>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            foreach (var projectId in component.ProjectIds)
            {
                if (!componentByProject.TryAdd(projectId, component))
                {
                    throw new InvalidOperationException($"Project '{projectId}' belongs to more than one component index entry.");
                }
            }
        }

        var edges = partitions
            .SelectMany(static partition => partition.Relations.Select(relation => (partition.Partition, Relation: relation)))
            .Where(entry => entry.Relation.TargetId is not null)
            .Select(entry => (
                Partition: entry.Partition,
                Relation: entry.Relation,
                Source: componentByProject.GetValueOrDefault(entry.Relation.SourceId),
                Target: componentByProject.GetValueOrDefault(entry.Relation.TargetId!)))
            .Where(static entry => entry.Source is not null && entry.Target is not null)
            .OrderBy(static entry => entry.Source!.ComponentId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Target!.ComponentId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Relation.RelationId, StringComparer.Ordinal)
            .ToImmutableArray();

        var nodes = edges
            .SelectMany(static edge => new[] { edge.Source!, edge.Target! })
            .DistinctBy(static component => component.ComponentId, StringComparer.Ordinal)
            .OrderBy(static component => component.ComponentId, StringComparer.Ordinal)
            .ToImmutableArray();
        var nodeIds = nodes.Select((component, index) => (component.ComponentId, NodeId: $"component{index}"))
            .ToDictionary(static entry => entry.ComponentId, static entry => entry.NodeId, StringComparer.Ordinal);

        var builder = new StringBuilder("flowchart LR\n");
        foreach (var node in nodes)
        {
            builder.Append("    ").Append(nodeIds[node.ComponentId]).Append("[\"")
                .Append(Escape(node.ComponentId)).Append("\"]\n");
        }

        foreach (var edge in edges)
        {
            builder.Append("    ").Append(nodeIds[edge.Source!.ComponentId])
                .Append(" -->|").Append(FactualJsonMapper.WireRelationPartition(edge.Partition)).Append(':').Append(Escape(edge.Relation.RelationKind))
                .Append("| ").Append(nodeIds[edge.Target!.ComponentId]).Append('\n');
        }

        return builder.ToString();
    }

    private static string ComponentIndex(ImmutableArray<ComponentIndexEntry> components)
    {
        var builder = new StringBuilder("# Components\n");
        foreach (var component in components)
        {
            builder.Append("\n## ").Append(component.ComponentKind).Append("\n\n")
                .Append("- id: `").Append(component.ComponentId).Append("`\n");
            foreach (var projectId in component.ProjectIds)
            {
                builder.Append("- project: `").Append(projectId).Append("`\n");
            }
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value
        .Replace("#", "#35;", StringComparison.Ordinal)
        .Replace("\"", "#quot;", StringComparison.Ordinal)
        .Replace("\r", string.Empty, StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);
}
