using System.Buffers;
using System.Text.Json;

namespace Csharp2Md.Core.Projection.Aggregates;

internal static class FactualFragmentScanner
{
    private const int InitialBufferSize = 4096;

    public static void ScanDocuments(Stream stream, string fragmentReference, Action<JsonElement> onDocument) =>
        Scan(stream, fragmentReference, "documents", stopAfterArray: true, ValidateDocument, onDocument);

    public static void ScanRelations(Stream stream, string fragmentReference, Action<JsonElement> onRelation) =>
        Scan(stream, fragmentReference, "relations", stopAfterArray: false, ValidateRelation, onRelation);

    private static void Scan(
        Stream stream,
        string fragmentReference,
        string arrayName,
        bool stopAfterArray,
        Action<JsonElement> validate,
        Action<JsonElement> onValue)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentReference);
        ArgumentNullException.ThrowIfNull(onValue);

        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        var retained = 0;
        var state = new JsonReaderState();
        var matchingArrays = 0;
        var currentArrayDepth = -1;
        var currentArrayCount = 0;
        var nonEmptyArrays = 0;
        string? pendingProperty = null;

        try
        {
            while (true)
            {
                if (retained == buffer.Length)
                {
                    var larger = ArrayPool<byte>.Shared.Rent(checked(buffer.Length * 2));
                    buffer.AsSpan(0, retained).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = larger;
                }

                var bytesRead = stream.Read(buffer, retained, buffer.Length - retained);
                var isFinalBlock = bytesRead == 0;
                var available = retained + bytesRead;
                var reader = new Utf8JsonReader(buffer.AsSpan(0, available), isFinalBlock, state);
                var committedBytes = 0L;
                var committedState = state;
                var needsMoreData = false;

                try
                {
                    while (true)
                    {
                        var stateBeforeRead = reader.CurrentState;
                        var bytesBeforeRead = reader.BytesConsumed;
                        if (!reader.Read())
                        {
                            committedBytes = reader.BytesConsumed;
                            committedState = reader.CurrentState;
                            break;
                        }

                        if (reader.TokenType is JsonTokenType.PropertyName)
                        {
                            pendingProperty = reader.GetString();
                        }
                        else if (reader.TokenType is JsonTokenType.StartArray &&
                                 reader.CurrentDepth == 1 &&
                                 string.Equals(pendingProperty, arrayName, StringComparison.Ordinal))
                        {
                            matchingArrays++;
                            currentArrayDepth = reader.CurrentDepth;
                            currentArrayCount = 0;
                            pendingProperty = null;
                        }
                        else if (currentArrayDepth >= 0 &&
                                 reader.CurrentDepth == currentArrayDepth + 1 &&
                                 reader.TokenType is JsonTokenType.StartObject)
                        {
                            var valueReader = reader;
                            if (!JsonDocument.TryParseValue(ref valueReader, out var value))
                            {
                                committedBytes = bytesBeforeRead;
                                committedState = stateBeforeRead;
                                needsMoreData = true;
                                break;
                            }

                            using (value)
                            {
                                validate(value.RootElement);
                                onValue(value.RootElement);
                            }

                            currentArrayCount++;
                            reader = valueReader;
                        }
                        else if (currentArrayDepth >= 0 &&
                                 reader.TokenType is JsonTokenType.EndArray &&
                                 reader.CurrentDepth == currentArrayDepth)
                        {
                            if (currentArrayCount > 0 && ++nonEmptyArrays > 1)
                            {
                                throw Error(fragmentReference, $"contains a second non-empty '{arrayName}' array");
                            }

                            currentArrayDepth = -1;
                            committedBytes = reader.BytesConsumed;
                            committedState = reader.CurrentState;
                            if (stopAfterArray)
                            {
                                return;
                            }
                        }

                        if (reader.TokenType is not JsonTokenType.PropertyName)
                        {
                            pendingProperty = null;
                        }

                        committedBytes = reader.BytesConsumed;
                        committedState = reader.CurrentState;
                    }
                }
                catch (JsonException exception) when (!exception.Message.Contains(fragmentReference, StringComparison.Ordinal))
                {
                    throw Error(fragmentReference, $"contains malformed JSON: {exception.Message}", exception);
                }

                var remaining = available - checked((int)committedBytes);
                if (remaining > 0)
                {
                    buffer.AsSpan(checked((int)committedBytes), remaining).CopyTo(buffer);
                }

                retained = remaining;
                state = committedState;

                if (isFinalBlock)
                {
                    if (needsMoreData || retained != 0)
                    {
                        throw Error(fragmentReference, "contains incomplete JSON");
                    }

                    if (matchingArrays == 0)
                    {
                        throw Error(fragmentReference, $"is missing required '{arrayName}' array");
                    }

                    return;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ValidateDocument(JsonElement value)
    {
        RequireObject(value, "document");
        RequireObject(value, "header");
        RequireString(value, "document_id");
        RequireString(value, "project_id");
        RequireString(value, "relative_path");
    }

    private static void ValidateRelation(JsonElement value)
    {
        RequireObject(value, "relation");
        var header = RequireObject(value, "header");
        RequireString(header, "id");
        RequireString(header, "resolution");
        RequireArray(header, "evidence");
        RequireArray(header, "provenance");
        RequireString(value, "relation_id");
        RequireString(value, "source_id");
        RequireString(value, "partition");
        RequireString(value, "relation_kind");
        RequireString(value, "resolution_method");
    }

    private static JsonElement RequireObject(JsonElement value, string propertyName)
    {
        if (propertyName is "document" or "relation")
        {
            if (value.ValueKind is JsonValueKind.Object)
            {
                return value;
            }
        }
        else if (value.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.Object)
        {
            return property;
        }

        throw new JsonException($"Required object '{propertyName}' is missing.");
    }

    private static void RequireString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"Required string '{propertyName}' is missing.");
        }
    }

    private static void RequireArray(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.Array)
        {
            throw new JsonException($"Required array '{propertyName}' is missing.");
        }
    }

    private static JsonException Error(string fragmentReference, string message, Exception? innerException = null) =>
        new($"Factual fragment '{fragmentReference}' {message}.", innerException);
}
