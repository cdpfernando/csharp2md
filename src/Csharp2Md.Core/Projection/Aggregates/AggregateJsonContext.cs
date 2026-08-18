using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

[JsonSerializable(typeof(AggregateEnvelope))]
[JsonSerializable(typeof(FactualManifest))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
internal partial class AggregateJsonContext : JsonSerializerContext;

