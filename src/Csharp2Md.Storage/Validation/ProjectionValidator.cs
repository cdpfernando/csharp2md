using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Validation;

public static class ProjectionValidator
{
    public static void Validate(PublishedPackageView view, ImmutableArray<StagedFragment> projections)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (projections.IsDefaultOrEmpty)
        {
            return;
        }

        var counts = view.Slots.ToDictionary(
            static slot => slot.CanonicalKey,
            static slot => slot.Count,
            StringComparer.Ordinal);
        var slotKeys = view.Slots
            .Select(static slot => slot.CanonicalKey)
            .ToHashSet(StringComparer.Ordinal);
        var sources = new Dictionary<string, ImmutableArray<byte>>(StringComparer.Ordinal);
        foreach (var fragment in projections)
        {
            counts.TryAdd(fragment.CanonicalKey, 1);
            if (!fragment.IsDeferred
                && !fragment.Payload.IsDefaultOrEmpty
                && fragment.CanonicalKey.StartsWith("source/", StringComparison.Ordinal))
            {
                sources[fragment.CanonicalKey] = fragment.Payload;
            }
        }

        foreach (var fragment in projections)
        {
            if (fragment.IsDeferred || fragment.Payload.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var link in EnumerateLinks(fragment.CanonicalKey, fragment.Payload))
            {
                if (!counts.TryGetValue(link.Citation.ArtifactKey, out var count))
                {
                    throw new PublicationRejectedException("projection-key", link.Citation.ArtifactKey);
                }

                if ((uint)link.Citation.Ordinal >= (uint)count)
                {
                    throw new PublicationRejectedException(
                        "projection-ordinal",
                        link.Citation.ArtifactKey + " " + link.Citation.Ordinal.ToString());
                }

                if (link.Span is { } span)
                {
                    EnsureSpanInsideSource(sources, link, span);
                }

                if (link.Value is { Length: > 0 } && slotKeys.Contains(link.Citation.ArtifactKey))
                {
                    EnsureValueMatches(view, link);
                }
            }
        }
    }

    private static void EnsureSpanInsideSource(
        Dictionary<string, ImmutableArray<byte>> sources,
        ProjectionLink link,
        SourceSpanDto span)
    {
        if (!sources.TryGetValue(link.Citation.ArtifactKey, out var bytes)
            || !SpanIsInside(bytes, span))
        {
            throw new PublicationRejectedException("projection-span", link.Locator);
        }
    }

    private static void EnsureValueMatches(PublishedPackageView view, ProjectionLink link)
    {
        var text = AuthoritativeText(view, link.Citation);
        if (!text.Contains(link.Value!, StringComparison.Ordinal))
        {
            throw new PublicationRejectedException("projection-value", link.Page + " " + link.Value);
        }
    }

    private static string AuthoritativeText(PublishedPackageView view, ArtifactCitation citation)
    {
        var bytes = PackagePublisher.Write(view.Document, citation.ArtifactKey);
        var node = JsonNode.Parse(bytes.AsSpan());
        var element = ElementAt(node, citation.Ordinal);
        return element?.ToJsonString() ?? Encoding.UTF8.GetString(bytes.AsSpan());
    }

    private static JsonNode? ElementAt(JsonNode? node, int ordinal)
    {
        switch (node)
        {
            case JsonArray array:
                return (uint)ordinal < (uint)array.Count ? array[ordinal] : node;
            case JsonObject obj:
                var items = new List<JsonNode?>();
                foreach (var property in obj)
                {
                    if (property.Value is JsonArray family)
                    {
                        items.AddRange(family);
                    }
                }

                if (items.Count > 0)
                {
                    return (uint)ordinal < (uint)items.Count ? items[ordinal] : null;
                }

                return ordinal == 0 ? node : null;
            default:
                return node;
        }
    }

    private static bool SpanIsInside(ImmutableArray<byte> utf8, SourceSpanDto span)
    {
        var text = Encoding.UTF8.GetString(utf8.AsSpan()).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = text.Split('\n');
        return PositionIsInside(lines, span.StartLine, span.StartColumn)
            && PositionIsInside(lines, span.EndLine, span.EndColumn)
            && (span.EndLine, span.EndColumn).CompareTo((span.StartLine, span.StartColumn)) >= 0;
    }

    private static bool PositionIsInside(string[] lines, int line, int column)
    {
        if (line < 1 || line > lines.Length)
        {
            return false;
        }

        return column >= 1 && column <= lines[line - 1].Length + 1;
    }

    private static IEnumerable<ProjectionLink> EnumerateLinks(string fragmentKey, ImmutableArray<byte> payload)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload.AsSpan());
        }
        catch (JsonException)
        {
            yield break;
        }

        foreach (var link in Walk(fragmentKey, node))
        {
            yield return link;
        }
    }

    private static IEnumerable<ProjectionLink> Walk(string fragmentKey, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (TryReadLink(fragmentKey, obj, out var link))
                {
                    yield return link;
                }

                foreach (var property in obj)
                {
                    foreach (var nested in Walk(fragmentKey, property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    foreach (var nested in Walk(fragmentKey, element))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static bool TryReadLink(string fragmentKey, JsonObject obj, out ProjectionLink link)
    {
        link = default;
        if (!obj.TryGetPropertyValue("artifact_key", out var keyNode)
            || keyNode is not JsonValue keyValue
            || !keyValue.TryGetValue<string>(out var key)
            || string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (!obj.TryGetPropertyValue("ordinal", out var ordinalNode)
            || ordinalNode is not JsonValue ordinalValue
            || !ordinalValue.TryGetValue<int>(out var ordinal))
        {
            return false;
        }

        var page = ReadString(obj, "page") ?? fragmentKey;
        var value = ReadString(obj, "value");
        var locator = ReadString(obj, "locator")
            ?? key + ":" + ordinal.ToString();
        link = new ProjectionLink(
            new ArtifactCitation(key, ordinal),
            page,
            value,
            locator,
            TryReadSpan(obj));
        return true;
    }

    private static SourceSpanDto? TryReadSpan(JsonObject obj)
    {
        if (!obj.TryGetPropertyValue("span", out var spanNode) || spanNode is not JsonObject span)
        {
            return null;
        }

        if (!TryReadInt(span, "start_line", out var startLine)
            || !TryReadInt(span, "start_column", out var startColumn)
            || !TryReadInt(span, "end_line", out var endLine)
            || !TryReadInt(span, "end_column", out var endColumn))
        {
            return null;
        }

        return new SourceSpanDto(startLine, startColumn, endLine, endColumn);
    }

    private static string? ReadString(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node)
        && node is JsonValue value
        && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static bool TryReadInt(JsonObject obj, string name, out int value)
    {
        value = 0;
        return obj.TryGetPropertyValue(name, out var node)
            && node is JsonValue jsonValue
            && jsonValue.TryGetValue(out value);
    }

    private readonly record struct ProjectionLink(
        ArtifactCitation Citation,
        string Page,
        string? Value,
        string Locator,
        SourceSpanDto? Span);
}
