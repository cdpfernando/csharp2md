using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Projection.Aggregates;

/// <summary>
/// One node in the component graph: a project component or a database object. <see cref="NodeId"/> is
/// the underlying identity (<c>ComponentFactId.Value</c> or <c>DatabaseObjectFactId.Value</c>) and is what
/// dedupe and tie-breaking use; <see cref="Label"/> is the display string
/// (<c>ProjectFact.Name</c> or <c>DatabaseObjectFact.Name</c>).
/// </summary>
internal sealed record GraphNode(string NodeId, string Label, GraphNodeShape Shape);

internal enum GraphNodeShape
{
    Component,
    DatabaseObject,
}

/// <summary>
/// Answers "which graph node owns this <see cref="FactId"/>?" in O(1) without ever interpreting a
/// <see cref="FactId"/>'s text - AD-014 defines ids as opaque references navigated through the facts the
/// pipeline already holds, not parsed. Every endpoint shape a <c>RelationFact</c> can carry is pre-expanded
/// into one flat map at build time: a project, document or symbol id all resolve to the same component
/// node its owning project produced; a database column id resolves to its owning object's node. Anything
/// else resolves to nothing, leaving the caller to omit the relation and count it.
/// </summary>
internal sealed class GraphNodeIndex
{
    private readonly FrozenDictionary<FactId, GraphNode> _nodesByFactId;

    private GraphNodeIndex(FrozenDictionary<FactId, GraphNode> nodesByFactId) => _nodesByFactId = nodesByFactId;

    public static GraphNodeIndex Build(
        IEnumerable<ComponentFact> components,
        IEnumerable<ProjectFact> projects,
        IEnumerable<DocumentFact> documents,
        IEnumerable<SymbolFact> symbols,
        IEnumerable<DatabaseObjectFact> objects,
        IEnumerable<DatabaseColumnFact> columns)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(columns);

        var projectNames = projects.ToDictionary(static project => project.ProjectId, static project => project.Name);

        // COMP-09: a project claimed by more than one component is a structural invariant violation, the
        // same one RelationProjector.Mermaid guarded before this index existed.
        var componentByProject = new Dictionary<ProjectFactId, GraphNode>();
        foreach (var component in components)
        {
            var node = new GraphNode(component.ComponentId.Value, ComponentLabel(component, projectNames), GraphNodeShape.Component);
            foreach (var projectId in component.ProjectIds)
            {
                if (!componentByProject.TryAdd(projectId, node))
                {
                    throw new InvalidOperationException(
                        $"Project '{projectId.Value}' belongs to more than one component index entry.");
                }
            }
        }

        var map = new Dictionary<FactId, GraphNode>();
        foreach (var (projectId, node) in componentByProject)
        {
            map[projectId.ToFactId()] = node;
        }

        var projectByDocument = new Dictionary<DocumentFactId, ProjectFactId>();
        foreach (var document in documents)
        {
            projectByDocument[document.DocumentId] = document.ProjectId;
            if (componentByProject.TryGetValue(document.ProjectId, out var node))
            {
                map[document.DocumentId.ToFactId()] = node;
            }
        }

        foreach (var symbol in symbols)
        {
            if (projectByDocument.TryGetValue(symbol.DocumentId, out var projectId)
                && componentByProject.TryGetValue(projectId, out var node))
            {
                map[symbol.SymbolId.ToFactId()] = node;
            }
        }

        var objectNodes = new Dictionary<DatabaseObjectFactId, GraphNode>();
        foreach (var databaseObject in objects)
        {
            var node = new GraphNode(databaseObject.ObjectId.Value, databaseObject.Name, GraphNodeShape.DatabaseObject);
            objectNodes[databaseObject.ObjectId] = node;
            map[databaseObject.ObjectId.ToFactId()] = node;
        }

        foreach (var column in columns)
        {
            if (objectNodes.TryGetValue(column.ObjectId, out var node))
            {
                map[column.ColumnId.ToFactId()] = node;
            }
        }

        return new GraphNodeIndex(map.ToFrozenDictionary());
    }

    /// <summary>The node <paramref name="endpoint"/> belongs to, or nothing when it maps to neither shape.</summary>
    public bool TryResolve(FactId endpoint, [NotNullWhen(true)] out GraphNode? node) =>
        _nodesByFactId.TryGetValue(endpoint, out node);

    private static string ComponentLabel(ComponentFact component, Dictionary<ProjectFactId, string> projectNames)
    {
        // COMP-01 guarantees exactly one owning project per component in this feature's P1 scope; service-
        // level components with several owners are P2 (COMP-40), out of scope here.
        var projectId = component.ProjectIds[0];
        return projectNames.TryGetValue(projectId, out var name) ? name : component.ComponentId.Value;
    }
}
