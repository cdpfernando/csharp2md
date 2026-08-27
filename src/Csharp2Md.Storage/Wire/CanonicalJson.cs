using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Csharp2Md.Storage.Wire;

public static class CanonicalJson
{
    public const string ContentSha256PropertyName = "content_sha256";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
    };

    public static ImmutableArray<byte> Write<T>(T dto)
    {
        var typeInfo = TypeInfo<T>();
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            JsonSerializer.Serialize(writer, dto, typeInfo);
        }

        return Normalize(buffer.WrittenSpan);
    }

    public static ImmutableArray<byte> WriteOmittingContentSha256<T>(T dto)
    {
        var node = JsonNode.Parse(Write(dto).AsSpan()) as JsonObject
            ?? throw new JsonException($"Canonical JSON for '{typeof(T)}' was not an object.");
        node.Remove(ContentSha256PropertyName);
        return WriteNode(node);
    }

    public static string PayloadContentSha256<T>(T dto)
    {
        var canonical = WriteOmittingContentSha256(dto);
        return Convert.ToHexStringLower(SHA256.HashData(canonical.AsSpan()));
    }

    public static T Read<T>(ReadOnlySpan<byte> utf8)
    {
        var value = JsonSerializer.Deserialize(utf8, TypeInfo<T>());
        return value ?? throw new JsonException($"Canonical JSON deserialized to null for '{typeof(T)}'.");
    }

    public static ImmutableArray<byte> Write(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return WriteNode(node);
    }

    private static ImmutableArray<byte> WriteNode(JsonNode node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            node.WriteTo(writer);
        }

        return Normalize(buffer.WrittenSpan);
    }

    private static ImmutableArray<byte> Normalize(ReadOnlySpan<byte> utf8)
    {
        var json = Utf8NoBom.GetString(utf8);
        json = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Utf8NoBom.GetBytes(json).ToImmutableArray();
    }

    private static JsonTypeInfo<T> TypeInfo<T>()
    {
        var info = StorageJsonContext.Default.GetTypeInfo(typeof(T));
        if (info is not JsonTypeInfo<T> typed)
        {
            throw new NotSupportedException($"'{typeof(T)}' is not registered on {nameof(StorageJsonContext)}.");
        }

        return typed;
    }
}
