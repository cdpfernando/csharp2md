using System.Text.Json;

namespace Csharp2Md.Core.Facts.Serialization;

public static class FactualJsonSerializer
{
    public const int SchemaVersion = 5;

    public static byte[] Serialize(FactualJsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != SchemaVersion)
        {
            throw new ArgumentException($"Factual schema version must be {SchemaVersion}.", nameof(document));
        }

        var canonical = Canonicalize(document);
        // A large aggregate fragment (e.g. the run's single relation or database fragment, AD-018) can
        // serialize to a JSON payload large enough that materializing it as a UTF-16 string first - the
        // string overload's own transcoding step - fails to allocate. Writing UTF-8 bytes directly skips
        // that intermediate string entirely.
        var utf8 = JsonSerializer.SerializeToUtf8Bytes(canonical, FactualJsonContext.Default.FactualJsonDocument);
        var normalized = NormalizeNewLines(utf8);
        return normalized is [.., (byte)'\n'] ? normalized : [.. normalized, (byte)'\n'];
    }

    /// <summary>
    /// Collapses CRLF to LF in place. Safe because System.Text.Json always escapes a string value's own
    /// \r/\n as the two-character sequences "\r"/"\n" - a raw CR-LF byte pair can only come from
    /// WriteIndented's own structural line breaks, never from content.
    /// </summary>
    private static byte[] NormalizeNewLines(byte[] utf8)
    {
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < utf8.Length; readIndex++)
        {
            if (utf8[readIndex] == (byte)'\r' && readIndex + 1 < utf8.Length && utf8[readIndex + 1] == (byte)'\n')
            {
                continue;
            }

            utf8[writeIndex++] = utf8[readIndex];
        }

        return writeIndex == utf8.Length ? utf8 : utf8[..writeIndex];
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
        DatabaseObjects = document.DatabaseObjects.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        DatabaseColumns = document.DatabaseColumns.OrderBy(static fact => fact.Header.Id, StringComparer.Ordinal).ToImmutableArray(),
        Diagnostics = document.Diagnostics.OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal).ToImmutableArray(),
        Coverage = document.Coverage
            .OrderBy(static coverage => coverage.ScopeId, StringComparer.Ordinal)
            .ThenBy(static coverage => coverage.DetectorId, StringComparer.Ordinal)
            .ThenBy(static coverage => coverage.FactLevel, StringComparer.Ordinal)
            .ToImmutableArray(),
    };
}
