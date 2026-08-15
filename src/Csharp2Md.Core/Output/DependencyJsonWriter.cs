using System.Text.Json;
using System.Text.Json.Serialization;
using Csharp2Md.Core.Graph;

namespace Csharp2Md.Core.Output;

/// <summary>
/// Serializes the dependency graph to <c>dependencies.json</c>: every edge with its source, target,
/// communication type and resolution classification (P2-11).
/// </summary>
/// <remarks>
/// Communication types and resolution kinds are written as the exact strings spec.md names
/// (<c>sincrono-bloqueante</c>, <c>direct-reference</c>, <c>hard-coded</c>, …) rather than as the
/// C# identifiers or as ordinals. The file is the tool's contract with whoever reads it, and an
/// ordinal would silently change meaning the day a value is inserted into the enum.
/// </remarks>
public static class DependencyJsonWriter
{
    public const string FileName = "dependencies.json";

    /// <summary>Writes <c>dependencies.json</c> beneath <paramref name="outputRoot"/> and returns its path.</summary>
    public static string Write(DependencyGraph graph, string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);

        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, FileName);
        File.WriteAllText(path, Serialize(graph));

        return path;
    }

    public static string Serialize(DependencyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var document = new DependencyGraphJson(
            graph.Edges
                .Select(edge => new DependencyEdgeJson(
                    edge.Source.Value,
                    edge.Target.Value,
                    edge.Communication,
                    edge.Resolution,
                    edge.Evidence.Select(location => location.ToString()).ToList()))
                .ToList());

        return JsonSerializer.Serialize(document, DependencyJsonContext.Default.DependencyGraphJson);
    }

    /// <summary>
    /// Reads back what <see cref="Serialize"/> wrote. Evidence locations are reconstructed from
    /// their <c>path:line</c> form; the split is on the <b>last</b> colon so a Windows drive letter
    /// stays part of the path.
    /// </summary>
    public static DependencyGraph Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize(json, DependencyJsonContext.Default.DependencyGraphJson);

        return new DependencyGraph(
            (document?.Edges ?? [])
                .Select(edge => new DependencyEdge(
                    new ServiceName(edge.Source),
                    new ServiceName(edge.Target),
                    edge.CommunicationType,
                    edge.Resolution,
                    edge.Evidence.Select(ParseLocation).ToList()))
                .ToList());
    }

    private static SourceLocation ParseLocation(string evidence)
    {
        var separator = evidence.LastIndexOf(':');

        return separator > 0 && int.TryParse(evidence[(separator + 1)..], out var line)
            ? new SourceLocation(evidence[..separator], line)
            : new SourceLocation(evidence, 0);
    }
}

/// <summary>The <c>dependencies.json</c> wire shape. P2-11 names the four required fields.</summary>
public sealed record DependencyEdgeJson(
    string Source,
    string Target,
    [property: JsonConverter(typeof(CommunicationTypeJsonConverter))] CommunicationType CommunicationType,
    [property: JsonConverter(typeof(ResolutionKindJsonConverter))] ResolutionKind Resolution,
    IReadOnlyList<string> Evidence);

public sealed record DependencyGraphJson(IReadOnlyList<DependencyEdgeJson> Edges);

/// <summary>Writes P2-12's five values in spec spelling: <c>sincrono-bloqueante</c> … <c>direct-reference</c>.</summary>
public sealed class CommunicationTypeJsonConverter()
    : JsonStringEnumConverter<CommunicationType>(JsonNamingPolicy.KebabCaseLower);

/// <summary>Writes P2-07/08/09's classifications as <c>hard-coded</c>, <c>dynamic</c>, <c>unresolved</c>.</summary>
public sealed class ResolutionKindJsonConverter()
    : JsonStringEnumConverter<ResolutionKind>(JsonNamingPolicy.KebabCaseLower);

[JsonSerializable(typeof(DependencyGraphJson))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
public partial class DependencyJsonContext : JsonSerializerContext
{
}
