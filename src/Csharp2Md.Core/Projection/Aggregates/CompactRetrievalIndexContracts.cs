using System.Text.Json;
using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record CompactRetrievalIndexManifest(
    int SchemaVersion, string AnalysisRunId, ManifestAnalysis Analysis, string Trust, bool RestorePerformed,
    ImmutableArray<RelationShardDescriptor> RelationShards,
    ImmutableArray<MetadataShardDescriptor> MetadataShards,
    ImmutableArray<PostingShardDescriptor> PostingShards,
    string SummaryPath, string UnknownsPath, string EntryPointsPath);

internal sealed record RelationShardDescriptor(string Path, int FirstOrdinal, int LastOrdinal, int EntryCount, int ByteLength);
internal sealed record MetadataShardDescriptor(string Kind, string Path, int FirstOrdinal, int LastOrdinal, int EntryCount, int ByteLength);
internal sealed record PostingShardDescriptor(string Family, string Path, string FirstKeyHash, string LastKeyHash, int EntryCount, int ByteLength);
internal sealed record CompactRelationRecord(string RelationId, int OriginOrdinal, string SourceId, string? TargetId, string Partition, string RelationKind, string Resolution, string ResolutionMethod, string? UnresolvedReason, JsonElement? Extensions);
internal sealed record CompactPostingList(string Key, ImmutableArray<int> RelationOrdinals);
internal sealed record CompactDocumentMetadata(string DocumentId, string? ProjectId, string RelativePath, bool GeneratedOrigin);
internal sealed record CompactOriginMetadata(string FragmentReference, string FragmentSha256, string GeneratorVersion);
