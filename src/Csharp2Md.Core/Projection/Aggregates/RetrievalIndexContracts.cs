using System.Text.Json;
using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record RetrievalIndexManifest(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string AnalysisRunId,
    [property: JsonPropertyOrder(2)] ManifestAnalysis Analysis,
    [property: JsonPropertyOrder(3)] string Trust,
    [property: JsonPropertyOrder(4)] bool RestorePerformed,
    [property: JsonPropertyOrder(5)] ImmutableArray<ShardDescriptor> Shards);

internal sealed record ShardDescriptor(
    [property: JsonPropertyOrder(0)] string Family,
    [property: JsonPropertyOrder(1)] string Key,
    [property: JsonPropertyOrder(2)] string Path,
    [property: JsonPropertyOrder(3)] int EntryCount,
    [property: JsonPropertyOrder(4)] int ByteLength);

internal sealed record RetrievalShard(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string AnalysisRunId,
    [property: JsonPropertyOrder(2)] string Family,
    [property: JsonPropertyOrder(3)] string Key,
    [property: JsonPropertyOrder(4)] ImmutableArray<RetrievalRelationEntry> Entries);

internal sealed record RetrievalRelationEntry(
    [property: JsonPropertyOrder(0)] string RelationId,
    [property: JsonPropertyOrder(1)] string FragmentReference,
    [property: JsonPropertyOrder(2)] string SourceId,
    [property: JsonPropertyOrder(3)] string? TargetId,
    [property: JsonPropertyOrder(4)] string? ProjectId,
    [property: JsonPropertyOrder(5)] string Partition,
    [property: JsonPropertyOrder(6)] string RelationKind,
    [property: JsonPropertyOrder(7)] string Resolution,
    [property: JsonPropertyOrder(8)] string ResolutionMethod,
    [property: JsonPropertyOrder(9)] string? UnresolvedReason,
    [property: JsonPropertyOrder(10)] string? ObservedTargetText,
    [property: JsonPropertyOrder(11)] ImmutableArray<RetrievalEvidence> Evidence,
    [property: JsonPropertyOrder(12)] JsonElement? Extensions);

internal sealed record RetrievalEvidence(
    [property: JsonPropertyOrder(0)] string DocumentId,
    [property: JsonPropertyOrder(1)] string RelativePath,
    [property: JsonPropertyOrder(2)] int StartLine,
    [property: JsonPropertyOrder(3)] int StartColumn,
    [property: JsonPropertyOrder(4)] int EndLine,
    [property: JsonPropertyOrder(5)] int EndColumn,
    [property: JsonPropertyOrder(6)] bool GeneratedOrigin,
    [property: JsonPropertyOrder(7)] string ProjectId,
    [property: JsonPropertyOrder(8)] string FragmentSha256,
    [property: JsonPropertyOrder(9)] string GeneratorVersion,
    [property: JsonPropertyOrder(10)] JsonElement? Extensions);

internal sealed record RetrievalQualityMetric(
    [property: JsonPropertyOrder(0)] string Key,
    [property: JsonPropertyOrder(1)] int Total,
    [property: JsonPropertyOrder(2)] int Exact,
    [property: JsonPropertyOrder(3)] int Dynamic,
    [property: JsonPropertyOrder(4)] int Unresolved,
    [property: JsonPropertyOrder(5)] int ExactPercent,
    [property: JsonPropertyOrder(6)] int DynamicPercent,
    [property: JsonPropertyOrder(7)] int UnresolvedPercent);

internal sealed record RetrievalUnknownGroup(
    [property: JsonPropertyOrder(0)] string UnresolvedReason,
    [property: JsonPropertyOrder(1)] string SourceId,
    [property: JsonPropertyOrder(2)] string ObservedTargetText,
    [property: JsonPropertyOrder(3)] int Count,
    [property: JsonPropertyOrder(4)] bool HasProvenEntryPoint,
    [property: JsonPropertyOrder(5)] int Impact,
    [property: JsonPropertyOrder(6)] ImmutableArray<string> RelationIds);

internal sealed record RetrievalIndexSummary(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string AnalysisRunId,
    [property: JsonPropertyOrder(2)] ManifestAnalysis Analysis,
    [property: JsonPropertyOrder(3)] string Trust,
    [property: JsonPropertyOrder(4)] bool RestorePerformed,
    [property: JsonPropertyOrder(5)] int IndexedSymbolCount,
    [property: JsonPropertyOrder(6)] int MappedEntryPointCount,
    [property: JsonPropertyOrder(7)] ImmutableArray<RetrievalQualityMetric> ByPartition,
    [property: JsonPropertyOrder(8)] ImmutableArray<RetrievalQualityMetric> ByRelationKind,
    [property: JsonPropertyOrder(9)] ImmutableArray<RetrievalUnknownGroup> UnknownGroups);
