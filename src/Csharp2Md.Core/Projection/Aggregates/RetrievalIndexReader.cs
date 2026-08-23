using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record RetrievalLookup(string Family, string Key);

/// <summary>Resolves schema-2 posting, relation and metadata shards behind one selective query interface.</summary>
internal sealed class RetrievalIndexReader
{
    private const int SchemaVersion = 2;
    private static readonly string[] Families = ["project", "source", "target", "kind", "resolution"];
    private readonly IAggregateFileReader _files;

    private RetrievalIndexReader(IAggregateFileReader files, CompactRetrievalIndexManifest manifest)
    {
        _files = files;
        Manifest = manifest;
    }

    public CompactRetrievalIndexManifest Manifest { get; }

    public static RetrievalIndexReader Open(IAggregateFileReader files, string manifestPath = "raw/index/manifest.json")
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var manifest = Read(files, manifestPath, CompactRetrievalIndexJsonContext.Default.CompactRetrievalIndexManifest);
        RequireSchema(manifest.SchemaVersion, manifestPath);
        RequireRunId(manifest.AnalysisRunId, manifestPath);
        return new RetrievalIndexReader(files, manifest);
    }

    public ImmutableArray<RetrievalRelationEntry> Query(RetrievalLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        if (!Families.Contains(lookup.Family, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(lookup), lookup.Family, "Unknown retrieval lookup family.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(lookup.Key);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lookup.Key)));
        var ordinals = new SortedSet<int>();
        foreach (var descriptor in Manifest.PostingShards.Where(descriptor =>
                     descriptor.Family == lookup.Family &&
                     StringComparer.Ordinal.Compare(descriptor.FirstKeyHash, hash) <= 0 &&
                     StringComparer.Ordinal.Compare(hash, descriptor.LastKeyHash) <= 0))
        {
            var shard = Read(_files, descriptor.Path, CompactRetrievalIndexJsonContext.Default.CompactPostingShard);
            ValidateArtifact(shard.SchemaVersion, shard.AnalysisRunId, descriptor.Path);
            if (shard.Family != lookup.Family)
            {
                throw Corrupt(lookup, descriptor.Path, $"contains family '{shard.Family}'");
            }

            foreach (var posting in shard.Entries.Where(posting => posting.Key == lookup.Key))
            {
                foreach (var ordinal in posting.RelationOrdinals)
                {
                    if (ordinal < 0 || !ordinals.Add(ordinal))
                    {
                        throw Corrupt(lookup, descriptor.Path, $"contains duplicate or invalid relation ordinal {ordinal}");
                    }
                }
            }
        }

        return Resolve(ordinals, $"family '{lookup.Family}', key '{lookup.Key}'");
    }

    public ImmutableArray<RetrievalUnknownGroup> ReadUnknownGroups()
    {
        var catalogue = Read(_files, Manifest.UnknownsPath, CompactRetrievalIndexJsonContext.Default.CompactUnknownCatalogue);
        ValidateArtifact(catalogue.SchemaVersion, catalogue.AnalysisRunId, Manifest.UnknownsPath);
        var result = ImmutableArray.CreateBuilder<RetrievalUnknownGroup>(catalogue.Entries.Length);
        foreach (var group in catalogue.Entries)
        {
            var relations = Resolve(group.RelationOrdinals, $"UNKNOWN source '{group.SourceId}'");
            if (group.Count != relations.Length)
            {
                throw new JsonException(
                    $"UNKNOWN source '{group.SourceId}' declares count {group.Count} but resolves {relations.Length} relations.");
            }

            result.Add(new RetrievalUnknownGroup(
                group.UnresolvedReason,
                group.SourceId,
                group.ObservedTargetText,
                group.Count,
                group.HasProvenEntryPoint,
                group.Impact,
                relations.Select(static relation => relation.RelationId).ToImmutableArray(),
                relations));
        }

        return result.ToImmutable();
    }

    private ImmutableArray<RetrievalRelationEntry> Resolve(IEnumerable<int> requestedOrdinals, string context)
    {
        var ordinals = requestedOrdinals.Order().ToImmutableArray();
        if (ordinals.Length != ordinals.Distinct().Count())
        {
            throw new JsonException($"Compact retrieval {context} contains a duplicate relation ordinal.");
        }

        var records = new SortedDictionary<int, CompactRelationRecord>();
        foreach (var descriptor in Manifest.RelationShards.Where(descriptor =>
                     ordinals.Any(ordinal => ordinal >= descriptor.FirstOrdinal && ordinal <= descriptor.LastOrdinal)))
        {
            var shard = Read(_files, descriptor.Path, CompactRetrievalIndexJsonContext.Default.CompactRelationShard);
            ValidateArtifact(shard.SchemaVersion, shard.AnalysisRunId, descriptor.Path);
            if (shard.FirstOrdinal != descriptor.FirstOrdinal ||
                shard.Entries.Length != descriptor.EntryCount ||
                descriptor.LastOrdinal != descriptor.FirstOrdinal + descriptor.EntryCount - 1)
            {
                throw new JsonException($"Relation shard '{descriptor.Path}' has inconsistent ordinal metadata.");
            }

            foreach (var ordinal in ordinals.Where(ordinal =>
                         ordinal >= descriptor.FirstOrdinal && ordinal <= descriptor.LastOrdinal))
            {
                if (!records.TryAdd(ordinal, shard.Entries[ordinal - shard.FirstOrdinal]))
                {
                    throw new JsonException($"Compact retrieval {context} maps relation ordinal {ordinal} more than once in '{descriptor.Path}'.");
                }
            }
        }

        foreach (var ordinal in ordinals)
        {
            if (!records.ContainsKey(ordinal))
            {
                throw new JsonException($"Compact retrieval {context} is missing relation ordinal {ordinal}.");
            }
        }

        var documentOrdinals = records.Values.SelectMany(static record => record.Evidence)
            .Select(static evidence => evidence.DocumentOrdinal).Distinct().Order().ToImmutableArray();
        var originOrdinals = records.Values.Select(static record => record.OriginOrdinal).Distinct().Order().ToImmutableArray();
        var documents = ReadDocuments(documentOrdinals, context);
        var origins = ReadOrigins(originOrdinals, context);
        return records.Select(pair => Reconstruct(pair.Key, pair.Value, documents, origins, context)).ToImmutableArray();
    }

    private Dictionary<int, CompactDocumentMetadata> ReadDocuments(ImmutableArray<int> ordinals, string context)
    {
        var result = new Dictionary<int, CompactDocumentMetadata>();
        foreach (var descriptor in MetadataDescriptors("documents", ordinals))
        {
            var shard = Read(_files, descriptor.Path, CompactRetrievalIndexJsonContext.Default.CompactDocumentMetadataShard);
            ValidateMetadata(shard.SchemaVersion, shard.AnalysisRunId, shard.Kind, shard.FirstOrdinal, descriptor);
            AddMetadata(shard.Entries, shard.FirstOrdinal, ordinals, result, context, descriptor.Path, "document");
        }

        RequireMetadata(ordinals, result, context, "document");
        return result;
    }

    private Dictionary<int, CompactOriginMetadata> ReadOrigins(ImmutableArray<int> ordinals, string context)
    {
        var result = new Dictionary<int, CompactOriginMetadata>();
        foreach (var descriptor in MetadataDescriptors("origins", ordinals))
        {
            var shard = Read(_files, descriptor.Path, CompactRetrievalIndexJsonContext.Default.CompactOriginMetadataShard);
            ValidateMetadata(shard.SchemaVersion, shard.AnalysisRunId, shard.Kind, shard.FirstOrdinal, descriptor);
            AddMetadata(shard.Entries, shard.FirstOrdinal, ordinals, result, context, descriptor.Path, "origin");
        }

        RequireMetadata(ordinals, result, context, "origin");
        return result;
    }

    private IEnumerable<MetadataShardDescriptor> MetadataDescriptors(string kind, ImmutableArray<int> ordinals) =>
        Manifest.MetadataShards.Where(descriptor => descriptor.Kind == kind &&
            ordinals.Any(ordinal => ordinal >= descriptor.FirstOrdinal && ordinal <= descriptor.LastOrdinal));

    private static void AddMetadata<T>(
        ImmutableArray<T> entries,
        int firstOrdinal,
        ImmutableArray<int> requested,
        Dictionary<int, T> result,
        string context,
        string path,
        string kind)
    {
        foreach (var ordinal in requested.Where(ordinal => ordinal >= firstOrdinal && ordinal < firstOrdinal + entries.Length))
        {
            if (!result.TryAdd(ordinal, entries[ordinal - firstOrdinal]))
            {
                throw new JsonException($"Compact retrieval {context} maps {kind} ordinal {ordinal} more than once in '{path}'.");
            }
        }
    }

    private static void RequireMetadata<T>(
        ImmutableArray<int> ordinals,
        IReadOnlyDictionary<int, T> values,
        string context,
        string kind)
    {
        foreach (var ordinal in ordinals)
        {
            if (!values.ContainsKey(ordinal))
            {
                throw new JsonException($"Compact retrieval {context} is missing {kind} ordinal {ordinal}.");
            }
        }
    }

    private RetrievalRelationEntry Reconstruct(
        int ordinal,
        CompactRelationRecord record,
        IReadOnlyDictionary<int, CompactDocumentMetadata> documents,
        IReadOnlyDictionary<int, CompactOriginMetadata> origins,
        string context)
    {
        if (!origins.TryGetValue(record.OriginOrdinal, out var origin))
        {
            throw new JsonException($"Compact retrieval {context} relation ordinal {ordinal} is missing origin ordinal {record.OriginOrdinal}.");
        }

        var evidence = record.Evidence.Select(value =>
        {
            if (!documents.TryGetValue(value.DocumentOrdinal, out var document))
            {
                throw new JsonException($"Compact retrieval {context} relation ordinal {ordinal} is missing document ordinal {value.DocumentOrdinal}.");
            }

            return new RetrievalEvidence(
                document.DocumentId,
                document.RelativePath,
                value.StartLine,
                value.StartColumn,
                value.EndLine,
                value.EndColumn,
                document.GeneratedOrigin,
                document.ProjectId,
                origin.FragmentSha256,
                origin.GeneratorVersion,
                value.Extensions);
        }).ToImmutableArray();
        var observedTarget = record.Details?.FirstOrDefault(static detail => detail.Key == "target_text")?.Value;
        return new RetrievalRelationEntry(
            record.RelationId,
            origin.FragmentReference,
            record.SourceId,
            record.TargetId,
            evidence.FirstOrDefault(static value => value.ProjectId is not null)?.ProjectId,
            record.Partition,
            record.RelationKind,
            record.Resolution,
            record.ResolutionMethod,
            record.UnresolvedReason,
            observedTarget,
            evidence,
            record.Extensions,
            record.Details,
            record.Candidates);
    }

    private void ValidateArtifact(int schemaVersion, string runId, string path)
    {
        RequireSchema(schemaVersion, path);
        if (runId != Manifest.AnalysisRunId)
        {
            throw new JsonException(
                $"Compact retrieval artifact '{path}' has analysis_run_id '{runId}', expected '{Manifest.AnalysisRunId}'.");
        }
    }

    private void ValidateMetadata(
        int schemaVersion,
        string runId,
        string kind,
        int firstOrdinal,
        MetadataShardDescriptor descriptor)
    {
        ValidateArtifact(schemaVersion, runId, descriptor.Path);
        if (kind != descriptor.Kind || firstOrdinal != descriptor.FirstOrdinal)
        {
            throw new JsonException($"Metadata shard '{descriptor.Path}' has inconsistent kind or ordinal metadata.");
        }
    }

    private static T Read<T>(IAggregateFileReader files, string path, JsonTypeInfo<T> typeInfo)
    {
        if (path.StartsWith("raw/facts/", StringComparison.Ordinal))
        {
            throw new JsonException($"Retrieval index reader cannot open factual path '{path}'.");
        }

        using var stream = files.OpenRead(path);
        return JsonSerializer.Deserialize(stream, typeInfo)
            ?? throw new JsonException($"Compact retrieval artifact '{path}' was null.");
    }

    private static void RequireSchema(int received, string path)
    {
        if (received != SchemaVersion)
        {
            throw new JsonException(
                $"Compact retrieval artifact '{path}' has schema_version {received}; expected {SchemaVersion}.");
        }
    }

    private static void RequireRunId(string runId, string path)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new JsonException($"Compact retrieval artifact '{path}' has an empty analysis_run_id.");
        }
    }

    private static JsonException Corrupt(RetrievalLookup lookup, string path, string detail) => new(
        $"Compact retrieval family '{lookup.Family}', key '{lookup.Key}', shard '{path}' {detail}.");

}
