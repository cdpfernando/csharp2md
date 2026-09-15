using System.Text.Json.Nodes;

namespace Csharp2Md.Storage.Wire;

/// <summary>
/// A per-artifact preamble of interned strings. <see cref="Encode"/> replaces every string leaf value
/// that repeats more than once across a JSON array of records with a reference into a table sorted
/// ordinally, removing the repeated identity, variant, classifier and facet strings that otherwise
/// appear on every record (GCPC-039). Ordering is a function of value frequency alone, so it never
/// depends on the order the records arrived in. An array with no repeated string produces an empty
/// table, so a caller can recognize interning bought nothing and skip publishing the wrapper
/// (GCPC-044's "no wasteful table" case).
/// </summary>
public static class InternTable
{
    private const string TablePropertyName = "table";
    private const string RecordsPropertyName = "records";
    private const string ReferencePropertyName = "$interned";

    /// <summary>
    /// Builds the interned form of <paramref name="records"/>: a JSON object carrying the sorted table
    /// of repeated strings and the records with those strings replaced by a reference to their table
    /// index.
    /// </summary>
    public static JsonObject Encode(JsonArray records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        CountStrings(records, counts);

        var table = counts
            .Where(static pair => pair.Value > 1)
            .Select(static pair => pair.Key)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        var index = new Dictionary<string, int>(table.Length, StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < table.Length; ordinal++)
        {
            index[table[ordinal]] = ordinal;
        }

        var tableArray = new JsonArray();
        foreach (var value in table)
        {
            tableArray.Add(JsonValue.Create(value));
        }

        return new JsonObject
        {
            [TablePropertyName] = tableArray,
            [RecordsPropertyName] = (JsonArray)Substitute(records, index)!,
        };
    }

    /// <summary>
    /// Reverses <see cref="Encode"/>, resolving every table reference back to its interned string and
    /// returning the original records array.
    /// </summary>
    public static JsonArray Decode(JsonObject encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        if (encoded[TablePropertyName] is not JsonArray tableNode)
        {
            throw new ArgumentException("Encoded artifact is missing its intern table.", nameof(encoded));
        }

        if (encoded[RecordsPropertyName] is not JsonArray recordsNode)
        {
            throw new ArgumentException("Encoded artifact is missing its records.", nameof(encoded));
        }

        var table = tableNode
            .Select(static node => node!.GetValue<string>())
            .ToArray();

        return (JsonArray)Restore(recordsNode, table)!;
    }

    private static void CountStrings(JsonNode? node, Dictionary<string, int> counts)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                counts[text] = counts.GetValueOrDefault(text) + 1;
                break;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    CountStrings(property.Value, counts);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    CountStrings(item, counts);
                }

                break;
        }
    }

    private static JsonNode? Substitute(JsonNode? node, Dictionary<string, int> index)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text) && index.TryGetValue(text, out var ordinal):
                return new JsonObject { [ReferencePropertyName] = ordinal };
            case JsonObject obj:
                var newObject = new JsonObject();
                foreach (var property in obj)
                {
                    newObject[property.Key] = Substitute(property.Value, index);
                }

                return newObject;
            case JsonArray array:
                var newArray = new JsonArray();
                foreach (var item in array)
                {
                    newArray.Add(Substitute(item, index));
                }

                return newArray;
            case null:
                return null;
            default:
                return node.DeepClone();
        }
    }

    private static JsonNode? Restore(JsonNode? node, string[] table)
    {
        switch (node)
        {
            case JsonObject obj
                when obj.Count == 1
                    && obj.TryGetPropertyValue(ReferencePropertyName, out var reference)
                    && reference is JsonValue referenceValue
                    && referenceValue.TryGetValue<int>(out var ordinal):
                return JsonValue.Create(table[ordinal]);
            case JsonObject obj:
                var newObject = new JsonObject();
                foreach (var property in obj)
                {
                    newObject[property.Key] = Restore(property.Value, table);
                }

                return newObject;
            case JsonArray array:
                var newArray = new JsonArray();
                foreach (var item in array)
                {
                    newArray.Add(Restore(item, table));
                }

                return newArray;
            case null:
                return null;
            default:
                return node.DeepClone();
        }
    }
}
