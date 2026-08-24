using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

[JsonSerializable(typeof(AggregateEnvelope))]
[JsonSerializable(typeof(DiagnosticAggregate))]
[JsonSerializable(typeof(CoverageAggregate))]
[JsonSerializable(typeof(RelationAggregate))]
[JsonSerializable(typeof(ResolutionMetricsAggregate))]
[JsonSerializable(typeof(DatabaseAggregate))]
[JsonSerializable(typeof(FactualManifest))]
[JsonSerializable(typeof(CompactRetrievalIndexManifest))]
[JsonSerializable(typeof(CompactRelationRecord))]
[JsonSerializable(typeof(CompactEvidence))]
[JsonSerializable(typeof(CompactPostingList))]
[JsonSerializable(typeof(CompactDocumentMetadata))]
[JsonSerializable(typeof(CompactOriginMetadata))]
[JsonSerializable(typeof(CompactRelationShard))]
[JsonSerializable(typeof(CompactDocumentMetadataShard))]
[JsonSerializable(typeof(CompactOriginMetadataShard))]
[JsonSerializable(typeof(CompactPostingShard))]
[JsonSerializable(typeof(CompactUnknownCatalogue))]
[JsonSerializable(typeof(CompactIndexSummary))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
internal partial class AggregateJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(CompactRetrievalIndexManifest))]
[JsonSerializable(typeof(CompactRelationRecord))]
[JsonSerializable(typeof(CompactEvidence))]
[JsonSerializable(typeof(CompactPostingList))]
[JsonSerializable(typeof(CompactDocumentMetadata))]
[JsonSerializable(typeof(CompactOriginMetadata))]
[JsonSerializable(typeof(CompactRelationShard))]
[JsonSerializable(typeof(CompactDocumentMetadataShard))]
[JsonSerializable(typeof(CompactOriginMetadataShard))]
[JsonSerializable(typeof(CompactPostingShard))]
[JsonSerializable(typeof(CompactUnknownCatalogue))]
[JsonSerializable(typeof(CompactIndexSummary))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class CompactRetrievalIndexJsonContext : JsonSerializerContext;
