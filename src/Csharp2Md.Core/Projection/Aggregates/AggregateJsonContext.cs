using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

[JsonSerializable(typeof(AggregateEnvelope))]
[JsonSerializable(typeof(DiagnosticAggregate))]
[JsonSerializable(typeof(CoverageAggregate))]
[JsonSerializable(typeof(RelationAggregate))]
[JsonSerializable(typeof(ResolutionMetricsAggregate))]
[JsonSerializable(typeof(DatabaseAggregate))]
[JsonSerializable(typeof(FactualManifest))]
[JsonSerializable(typeof(RetrievalIndexManifest))]
[JsonSerializable(typeof(RetrievalShard))]
[JsonSerializable(typeof(RetrievalIndexSummary))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
internal partial class AggregateJsonContext : JsonSerializerContext;
