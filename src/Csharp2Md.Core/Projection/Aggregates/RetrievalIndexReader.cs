using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Csharp2Md.Core.Projection.Aggregates;

/// <summary>Reads versioned retrieval-index documents for selective consumers.</summary>
internal static class RetrievalIndexReader
{
    public static RetrievalIndexManifest ReadManifest(ReadOnlySpan<byte> utf8Json) =>
        Deserialize(utf8Json, LegacyRetrievalIndexJsonContext.Default.RetrievalIndexManifest, "manifest");

    public static RetrievalShard ReadShard(ReadOnlySpan<byte> utf8Json) =>
        Deserialize(utf8Json, LegacyRetrievalIndexJsonContext.Default.RetrievalShard, "shard");

    public static RetrievalIndexSummary ReadSummary(ReadOnlySpan<byte> utf8Json) =>
        Deserialize(utf8Json, LegacyRetrievalIndexJsonContext.Default.RetrievalIndexSummary, "summary");

    private static T Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonTypeInfo<T> typeInfo, string documentName) =>
        JsonSerializer.Deserialize(utf8Json, typeInfo)
        ?? throw new JsonException($"The retrieval index {documentName} was null.");
}

[JsonSerializable(typeof(RetrievalIndexManifest))]
[JsonSerializable(typeof(RetrievalShard))]
[JsonSerializable(typeof(RetrievalIndexSummary))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial class LegacyRetrievalIndexJsonContext : JsonSerializerContext;
