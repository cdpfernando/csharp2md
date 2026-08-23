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
[JsonSerializable(typeof(RetrievalUnknownCatalogue))]
[JsonSerializable(typeof(CompactRetrievalIndexManifest))]
[JsonSerializable(typeof(CompactRelationRecord))]
[JsonSerializable(typeof(CompactPostingList))]
[JsonSerializable(typeof(CompactDocumentMetadata))]
[JsonSerializable(typeof(CompactOriginMetadata))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
internal partial class AggregateJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(CompactRetrievalIndexManifest))]
[JsonSerializable(typeof(CompactRelationRecord))]
[JsonSerializable(typeof(CompactPostingList))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class CompactRetrievalIndexJsonContext : JsonSerializerContext;
