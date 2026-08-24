using System.Globalization;
using System.Text;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Storage;
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
    /// <summary>COMP-25: one summary diagnostic per run, never one per omitted relation.</summary>
    private const string OmittedRelationCode = "C2M-CG-001";

    public static ComponentGraphProjection Project(IEnumerable<ValidatedFactFragment> fragments, GraphNodeIndex nodes)
    {
        ArgumentNullException.ThrowIfNull(fragments);
        ArgumentNullException.ThrowIfNull(nodes);

        var materialized = fragments.Select(static fragment =>
        {
            ArgumentNullException.ThrowIfNull(fragment);
            return fragment;
        }).ToImmutableArray();

        var (edges, omittedForUnmappedEndpoint) = SelectEdges(materialized, nodes);
        var components = SelectComponents(materialized);
        var diagnostics = OmissionDiagnostics(omittedForUnmappedEndpoint);

        return new ComponentGraphProjection(edges, RenderMermaid(edges), RenderComponentIndex(components), diagnostics);
    }

    /// <summary>
    /// COMP-25/COMP-26: relations dropped only because an endpoint mapped to no node get exactly one
    /// summary diagnostic, anchored on the ordinal-first such relation's own id so the anchor stays stable
    /// across two runs whose input arrived in a different order - the same representative-fact anchoring
    /// pattern <c>DatabaseAggregateProjector.CaseCollisions</c> uses. A self-edge or a null target is
    /// specified behaviour rather than a gap (COMP-26), so neither ever reaches this list.
    /// </summary>
    private static ImmutableArray<AnalysisDiagnostic> OmissionDiagnostics(ImmutableArray<RelationFact> omittedForUnmappedEndpoint)
    {
        if (omittedForUnmappedEndpoint.IsEmpty)
        {
            return [];
        }

        var anchor = omittedForUnmappedEndpoint
            .OrderBy(static relation => relation.Header.Id.Value, StringComparer.Ordinal)
            .First();

        return
        [
            AnalysisDiagnostic.Create(
                OmittedRelationCode,
                DiagnosticSeverity.Information,
                DiagnosticStage.Projection,
                anchor.Header.Id,
                "One or more resolved relations were omitted from the component graph because an endpoint mapped to no node.",
                [new DiagnosticData("omitted_relation_count", omittedForUnmappedEndpoint.Length.ToString(CultureInfo.InvariantCulture))]),
        ];
    }

    /// <summary>
    /// Every <see cref="ComponentFact"/> the run's fragments carry, ordered by component id (COMP-05).
    /// Database objects are never <see cref="ComponentFact"/>s, so they are absent from this list by
    /// construction rather than by an explicit filter (COMP-24).
    /// </summary>
    private static ImmutableArray<ComponentFact> SelectComponents(IEnumerable<ValidatedFactFragment> fragments) =>
        fragments
            .SelectMany(static fragment => fragment.Facts.OfType<ComponentFact>())
            .OrderBy(static component => component.ComponentId.Value, StringComparer.Ordinal)
            .ToImmutableArray();

    /// <summary>Moved from <c>RelationProjector.ComponentIndex</c> - the Markdown shape is unchanged.</summary>
    private static string RenderComponentIndex(ImmutableArray<ComponentFact> components)
    {
        var builder = new StringBuilder("# Components\n");
        foreach (var component in components)
        {
            builder.Append("\n## ").Append(component.ComponentKind).Append("\n\n")
                .Append("- id: `").Append(component.ComponentId.Value).Append("`\n");
            foreach (var projectId in component.ProjectIds)
            {
                builder.Append("- project: `").Append(projectId.Value).Append("`\n");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders a node line for every node an edge touches and nothing else (COMP-15), then an edge line
    /// per deduped edge carrying its collapsed count (COMP-11). A project component renders as a
    /// rectangle, a database object as Mermaid's cylinder (COMP-20, COMP-23). Node aliases are positional
    /// (<c>node0</c>, <c>node1</c>, ...) but assigned after a canonical ordering (COMP-16), so two runs
    /// over identical input always write byte-identical output regardless of the order relations were
    /// discovered in.
    /// </summary>
    private static string RenderMermaid(ImmutableArray<ComponentGraphEdge> edges)
    {
        var touchedNodes = edges
            .SelectMany(static edge => new[] { edge.Source, edge.Target })
            .Distinct()
            // COMP-16/COMP-30: Label alone is not a total order - two database objects on different
            // connections may share a Name - so NodeId (the object's or component's real identity) breaks
            // the tie rather than leaving the order input-dependent.
            .OrderBy(static node => node.Label, StringComparer.Ordinal)
            .ThenBy(static node => node.NodeId, StringComparer.Ordinal)
            .ToImmutableArray();
        var aliases = touchedNodes
            .Select(static (node, index) => (node, alias: $"node{index}"))
            .ToDictionary(static entry => entry.node, static entry => entry.alias);

        var orderedEdges = edges
            .OrderBy(static edge => edge.Source.Label, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Source.NodeId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Target.Label, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Target.NodeId, StringComparer.Ordinal)
            .ThenBy(static edge => FactualJsonMapper.WireRelationPartition(edge.Partition), StringComparer.Ordinal)
            .ThenBy(static edge => edge.RelationKind, StringComparer.Ordinal)
            .ToImmutableArray();

        var builder = new StringBuilder("flowchart LR\n");
        foreach (var node in touchedNodes)
        {
            builder.Append("    ").Append(aliases[node]).Append('[').Append(Bracket(node)).Append("]\n");
        }

        foreach (var edge in orderedEdges)
        {
            builder.Append("    ").Append(aliases[edge.Source])
                .Append(" -->|").Append(FactualJsonMapper.WireRelationPartition(edge.Partition)).Append(':').Append(Escape(edge.RelationKind))
                .Append(" ×").Append(edge.Count).Append("| ").Append(aliases[edge.Target]).Append('\n');
        }

        return builder.ToString();
    }

    private static string Bracket(GraphNode node) => node.Shape switch
    {
        GraphNodeShape.DatabaseObject => $"(\"{Escape(node.Label)}\")",
        _ => $"\"{Escape(node.Label)}\"",
    };

    /// <summary>
    /// Moved from <c>RelationProjector.Escape</c> and extended with the <c>|</c> rule (COMP-18): unlike
    /// the relation projector's Mermaid, an edge label here always sits between two pipes, so an
    /// unescaped <c>|</c> in a relation kind would break the Mermaid edge syntax.
    /// </summary>
    private static string Escape(string value) => value
        .Replace("#", "#35;", StringComparison.Ordinal)
        .Replace("\"", "#quot;", StringComparison.Ordinal)
        .Replace("|", "#124;", StringComparison.Ordinal)
        .Replace("\r", string.Empty, StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    /// <summary>
    /// Applies COMP-12..14's three drop rules to every relation the run resolved - a null target, a
    /// self-edge, and an endpoint that maps to no node are each omitted for their own reason - then groups
    /// the survivors by (source node, target node, partition, relation kind) into one deduped edge apiece
    /// (COMP-11, COMP-19). A column-targeted relation folds into the same group as an object-targeted
    /// relation of the same kind for free, because <see cref="GraphNodeIndex"/> already maps both a column
    /// id and its owning object's id to the identical node (COMP-21, COMP-22). Relations dropped for an
    /// unmapped endpoint are returned alongside the edges so <see cref="OmissionDiagnostics"/> can
    /// summarise them (COMP-25); a self-edge or a null target is specified behaviour and is never
    /// collected (COMP-26).
    /// </summary>
    private static (ImmutableArray<ComponentGraphEdge> Edges, ImmutableArray<RelationFact> OmittedForUnmappedEndpoint) SelectEdges(
        IEnumerable<ValidatedFactFragment> fragments, GraphNodeIndex nodes)
    {
        var groups = new Dictionary<
            (string Source, string Target, RelationPartition Partition, string Kind),
            (GraphNode Source, GraphNode Target, int Count)>();
        var omittedForUnmappedEndpoint = ImmutableArray.CreateBuilder<RelationFact>();

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
                    omittedForUnmappedEndpoint.Add(relation); // COMP-14/COMP-25: unmapped endpoint, counted.
                    continue;
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

        var edges = groups
            .Select(static entry => new ComponentGraphEdge(
                entry.Value.Source, entry.Value.Target, entry.Key.Partition, entry.Key.Kind, entry.Value.Count))
            .ToImmutableArray();

        return (edges, omittedForUnmappedEndpoint.ToImmutable());
    }
}
