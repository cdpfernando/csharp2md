using System.Text.Json;
using Csharp2Md.Core.Facts.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record CompactRetrievalIndexManifest(
    int SchemaVersion,
    string AnalysisRunId,
    ManifestAnalysis Analysis,
    string Trust,
    bool RestorePerformed,
    ImmutableArray<RelationShardDescriptor> RelationShards,
    ImmutableArray<MetadataShardDescriptor> MetadataShards,
    ImmutableArray<PostingShardDescriptor> PostingShards,
    string SummaryPath,
    string UnknownsPath,
    string EntryPointsPath);

internal sealed record RelationShardDescriptor(
    string Path,
    int FirstOrdinal,
    int LastOrdinal,
    int EntryCount,
    int ByteLength);

internal sealed record MetadataShardDescriptor(
    string Kind,
    string Path,
    int FirstOrdinal,
    int LastOrdinal,
    int EntryCount,
    int ByteLength);

internal sealed record PostingShardDescriptor(
    string Family,
    string Path,
    string FirstKeyHash,
    string LastKeyHash,
    int EntryCount,
    int ByteLength);

internal sealed record CompactRelationRecord(
    string RelationId,
    int OriginOrdinal,
    string SourceId,
    string? TargetId,
    string Partition,
    string RelationKind,
    string Resolution,
    string ResolutionMethod,
    string? UnresolvedReason,
    ImmutableArray<RelationDetailJson>? Details,
    ImmutableArray<string>? Candidates,
    ImmutableArray<CompactEvidence> Evidence,
    JsonElement? Extensions);

internal sealed record CompactEvidence(
    int DocumentOrdinal,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    JsonElement? Extensions);

internal sealed record CompactPostingList(string Key, ImmutableArray<int> RelationOrdinals);
internal sealed record CompactDocumentMetadata(string DocumentId, string? ProjectId, string RelativePath, bool GeneratedOrigin);
internal sealed record CompactOriginMetadata(string FragmentReference, string FragmentSha256, string GeneratorVersion);

internal sealed record CompactRelationShard(
    int SchemaVersion,
    string AnalysisRunId,
    int FirstOrdinal,
    ImmutableArray<CompactRelationRecord> Entries);

internal sealed record CompactDocumentMetadataShard(
    int SchemaVersion,
    string AnalysisRunId,
    string Kind,
    int FirstOrdinal,
    ImmutableArray<CompactDocumentMetadata> Entries);

internal sealed record CompactOriginMetadataShard(
    int SchemaVersion,
    string AnalysisRunId,
    string Kind,
    int FirstOrdinal,
    ImmutableArray<CompactOriginMetadata> Entries);

internal sealed record CompactPostingShard(
    int SchemaVersion,
    string AnalysisRunId,
    string Family,
    ImmutableArray<CompactPostingList> Entries);

internal sealed record CompactUnknownCatalogue(
    int SchemaVersion,
    string AnalysisRunId,
    ImmutableArray<CompactUnknownGroup> Entries);

internal sealed record CompactIndexSummary(
    int SchemaVersion,
    string AnalysisRunId,
    int IndexedEndpointCount,
    int MappedEntryPointCount,
    int UnknownGroupCount,
    string UnknownsPath,
    ImmutableArray<CompactQualityMetric> ByPartition,
    ImmutableArray<CompactQualityMetric> ByRelationKind,
    ImmutableArray<string> AnalysisLimitations);
