using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record ComponentGraphEdge(
    GraphNode Source,
    GraphNode Target,
    RelationPartition Partition,
    string RelationKind,
    int Count);

internal sealed record ComponentGraphProjection(
    ImmutableArray<ComponentGraphEdge> Edges,
    string Mermaid,
    string ComponentIndex,
    ImmutableArray<AnalysisDiagnostic> Diagnostics)
{
    public static ComponentGraphProjection Empty { get; } = new([], "flowchart LR\n", "# Components\n", []);
}

/// <summary>
/// Renders the deduped component graph and the component index from already validated fragments. Reading
/// only validated fragments - never a raw <c>RelationResolution</c> - makes it structurally impossible for
/// an unvalidated relation to reach the diagram, the same discipline <c>RelationProjector.Project</c>
/// already follows for the partition files.
/// </summary>
internal static class ComponentGraphProjector
{
    public static ComponentGraphProjection Project(IEnumerable<ValidatedFactFragment> fragments, GraphNodeIndex nodes)
    {
        ArgumentNullException.ThrowIfNull(fragments);
        ArgumentNullException.ThrowIfNull(nodes);

        var edges = SelectEdges(fragments, nodes);

        return new ComponentGraphProjection(edges, "flowchart LR\n", "# Components\n", []);
    }

    /// <summary>
    /// Applies COMP-12..14's three drop rules to every relation the run resolved - a null target, a
    /// self-edge, and an endpoint that maps to no node are each omitted for their own reason - then groups
    /// the survivors by (source node, target node, partition, relation kind) into one deduped edge apiece
    /// (COMP-11, COMP-19). A column-targeted relation folds into the same group as an object-targeted
    /// relation of the same kind for free, because <see cref="GraphNodeIndex"/> already maps both a column
    /// id and its owning object's id to the identical node (COMP-21, COMP-22).
    /// </summary>
    private static ImmutableArray<ComponentGraphEdge> SelectEdges(
        IEnumerable<ValidatedFactFragment> fragments, GraphNodeIndex nodes)
    {
        var groups = new Dictionary<
            (string Source, string Target, RelationPartition Partition, string Kind),
            (GraphNode Source, GraphNode Target, int Count)>();

        foreach (var fragment in fragments)
        {
            ArgumentNullException.ThrowIfNull(fragment);
            foreach (var relation in fragment.Facts.OfType<RelationFact>())
            {
                if (relation.TargetId is not { } targetId)
                {
                    continue; // COMP-13/COMP-26: no target - specified behaviour, no diagnostic.
                }

                if (!nodes.TryResolve(relation.SourceId, out var source) || !nodes.TryResolve(targetId, out var target))
                {
                    continue; // COMP-14/COMP-25: unmapped endpoint - summarised in one diagnostic elsewhere.
                }

                if (string.Equals(source!.NodeId, target!.NodeId, StringComparison.Ordinal))
                {
                    continue; // COMP-12/COMP-26: self-edge - specified behaviour, no diagnostic.
                }

                var key = (source.NodeId, target.NodeId, relation.Partition, relation.RelationKind);
                groups[key] = groups.TryGetValue(key, out var existing)
                    ? (existing.Source, existing.Target, existing.Count + 1)
                    : (source, target, 1);
            }
        }

        return groups
            .Select(static entry => new ComponentGraphEdge(
                entry.Value.Source, entry.Value.Target, entry.Key.Partition, entry.Key.Kind, entry.Value.Count))
            .ToImmutableArray();
    }
}
