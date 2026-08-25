using System.Buffers;
using System.Text;
using System.Text.Json;

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
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            JsonSerializer.Serialize(writer, dto);
        }

        var json = Utf8NoBom.GetString(buffer.WrittenSpan);
        json = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Utf8NoBom.GetBytes(json).ToImmutableArray();
    }

    public static T Read<T>(ReadOnlySpan<byte> utf8)
    {
        var value = JsonSerializer.Deserialize<T>(utf8);
        return value ?? throw new JsonException($"Canonical JSON deserialized to null for '{typeof(T)}'.");
    }
}
