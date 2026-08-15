using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Output;

/// <summary>
/// Renders the dependency graph as a Mermaid flowchart in which every edge is labeled with its
/// communication type (P2-13).
/// </summary>
/// <remarks>
/// Node identifiers are generated (<c>svc0</c>, <c>svc1</c>, …) rather than derived from service
/// names, because a target may be a raw address or topic name (AD-005) containing dots, colons and
/// slashes that Mermaid will not accept as an identifier. The readable name lives in the node's
/// quoted label instead, where it only has to survive escaping.
/// </remarks>
public static class MermaidWriter
{
    public const string FileName = "dependencies.mmd";

    /// <summary>Writes <c>dependencies.mmd</c> beneath <paramref name="outputRoot"/> and returns its path.</summary>
    public static string Write(DependencyGraph graph, string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);

        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, FileName);
        File.WriteAllText(path, Render(graph));

        return path;
    }

    public static string Render(DependencyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var builder = new StringBuilder("graph LR\n");
        var ids = new Dictionary<ServiceName, string>();

        // Declared in first-appearance order over an edge list the GraphBuilder already sorted, so
        // the diagram is byte-stable across runs.
        foreach (var name in graph.Edges.SelectMany(edge => new[] { edge.Source, edge.Target }))
        {
            if (ids.ContainsKey(name))
            {
                continue;
            }

            ids[name] = $"svc{ids.Count}";
            builder.Append("    ").Append(ids[name]).Append("[\"").Append(Escape(name.Value)).Append("\"]\n");
        }

        foreach (var edge in graph.Edges)
        {
            builder.Append("    ")
                .Append(ids[edge.Source])
                .Append(" -->|")
                .Append(Escape(Label(edge.Communication)))
                .Append("| ")
                .Append(ids[edge.Target])
                .Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// P2-13's label, in spec.md's own spelling. Derived from the same naming policy
    /// <see cref="DependencyJsonWriter"/> serializes with, so the diagram and
    /// <c>dependencies.json</c> cannot drift into two different vocabularies.
    /// </summary>
    public static string Label(CommunicationType communication) =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(communication.ToString());

    /// <summary>
    /// Mermaid reads <c>"</c> as the end of a quoted label and <c>#</c> as the start of an entity;
    /// either one appearing raw in a service name breaks the whole diagram. Both become entities,
    /// <c>#</c> first so the replacement of <c>"</c> is not re-encoded.
    /// </summary>
    private static string Escape(string text) => text
        .Replace("#", "#35;", StringComparison.Ordinal)
        .Replace("\"", "#quot;", StringComparison.Ordinal)
        .Replace("\r", string.Empty, StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);
}
