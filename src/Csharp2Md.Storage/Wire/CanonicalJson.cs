using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Csharp2Md.Storage.Wire;

public static class CanonicalJson
{
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

        var json = Utf8NoBom.GetString(buffer.WrittenSpan);
        json = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Utf8NoBom.GetBytes(json).ToImmutableArray();
    }

    public static T Read<T>(ReadOnlySpan<byte> utf8)
    {
        var value = JsonSerializer.Deserialize(utf8, TypeInfo<T>());
        return value ?? throw new JsonException($"Canonical JSON deserialized to null for '{typeof(T)}'.");
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
