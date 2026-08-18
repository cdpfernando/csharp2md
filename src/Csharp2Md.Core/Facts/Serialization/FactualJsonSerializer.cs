using System.Text;
using System.Text.Json;

namespace Csharp2Md.Core.Facts.Serialization;

public static class FactualJsonSerializer
{
    public const int SchemaVersion = 2;

    public static byte[] Serialize(FactualJsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != SchemaVersion)
        {
            throw new ArgumentException($"Factual schema version must be {SchemaVersion}.", nameof(document));
        }

        var canonical = Canonicalize(document);
        var json = JsonSerializer.Serialize(canonical, FactualJsonContext.Default.FactualJsonDocument)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(json.EndsWith('\n') ? json : json + '\n');
    }

    public static FactualJsonDocument Deserialize(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize(utf8Json, FactualJsonContext.Default.FactualJsonDocument)
        ?? throw new JsonException("The factual JSON document was null.");

    private static FactualJsonDocument Canonicalize(FactualJsonDocument document) => document with
    {
        Solutions = document.Solutions.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Projects = document.Projects.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Targets = document.Targets.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Documents = document.Documents.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        SourceSections = document.SourceSections.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Symbols = document.Symbols.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Components = document.Components.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Relations = document.Relations.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Diagnostics = document.Diagnostics.OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal).ToImmutableArray(),
        Coverage = document.Coverage
            .OrderBy(static coverage => coverage.ScopeId, StringComparer.Ordinal)
            .ThenBy(static coverage => coverage.DetectorId, StringComparer.Ordinal)
            .ThenBy(static coverage => coverage.FactLevel, StringComparer.Ordinal)
            .ToImmutableArray(),
    };
}
