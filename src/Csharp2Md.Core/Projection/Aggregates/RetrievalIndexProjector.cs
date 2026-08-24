using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Csharp2Md.Core.Facts.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed class RetrievalIndexProjector(BoundedUtf8ShardWriter? shardWriter = null)
{
    private const int SchemaVersion = 2;
    private const string SummaryPath = "raw/index/summary.json";
    private const string UnknownsPath = "raw/index/unknowns.json";
    private const string EntryPointsPath = "raw/index/catalogues/entry-points.json";
    private readonly BoundedUtf8ShardWriter _shardWriter = shardWriter ?? new BoundedUtf8ShardWriter();

    public CompactRetrievalIndexProjection Project(FactualManifest manifest, IAggregateFileWriter files)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(files);

        var runId = ComputeAnalysisRunId(manifest);
        var builder = new CompactRetrievalIndexBuilder();
        ReadDocuments(manifest, files, builder);
        var parsedRelations = ReadRelations(manifest, files, builder);
        var built = builder.Build();
        var documentOrdinals = built.Documents.Select((value, ordinal) => (value.DocumentId, ordinal))
            .ToDictionary(static item => item.DocumentId, static item => item.ordinal, StringComparer.Ordinal);
        var originOrdinals = built.Origins.Select((value, ordinal) => (value, ordinal))
            .ToDictionary(static item => item.value, static item => item.ordinal);

        var relationRecords = parsedRelations.Select(relation => Serialize(
            relation.ToRecord(originOrdinals, documentOrdinals),
            CompactRetrievalIndexJsonContext.Default.CompactRelationRecord)).ToImmutableArray();
        var relationDescriptors = WriteRelationShards(relationRecords, parsedRelations, runId, files);
        var metadataDescriptors = WriteMetadataShards(built, runId, files);
        var postingDescriptors = WritePostingShards(built.Postings, runId, files);

        var limitations = manifest.Analysis.Effective == "syntax-only"
            ? ImmutableArray.Create("compile-time", "dependency-injection", "grpc")
            : [];
        var summary = new CompactIndexSummary(
            SchemaVersion,
            runId,
            built.Metrics.IndexedEndpointCount,
            built.Metrics.MappedEntryPointCount,
            built.UnknownGroups.Length,
            UnknownsPath,
            built.Metrics.ByPartition,
            built.Metrics.ByRelationKind,
            limitations);
        Write(UnknownsPath, new CompactUnknownCatalogue(SchemaVersion, runId, built.UnknownGroups),
            CompactRetrievalIndexJsonContext.Default.CompactUnknownCatalogue, files);
        Write(SummaryPath, summary, CompactRetrievalIndexJsonContext.Default.CompactIndexSummary, files);
        var entryPoints = parsedRelations
            .Where(static relation => relation.RelationKind == "aspnet-entrypoint" && relation.Resolution == "exact")
            .Select(static relation => relation.SourceId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        WriteEntryPoints(entryPoints, runId, files);

        var indexManifest = new CompactRetrievalIndexManifest(
            SchemaVersion,
            runId,
            manifest.Analysis,
            manifest.Trust,
            manifest.RestorePerformed,
            relationDescriptors,
            metadataDescriptors,
            postingDescriptors,
            SummaryPath,
            UnknownsPath,
            EntryPointsPath);
        Write("raw/index/manifest.json", indexManifest,
            CompactRetrievalIndexJsonContext.Default.CompactRetrievalIndexManifest, files);

        var counters = new CompactSerializationCounters(
            relationRecords.Length,
            built.Documents.Length + built.Origins.Length,
            postingDescriptors.Sum(static descriptor => descriptor.EntryCount),
            AllPostingLists(built.Postings).Sum(static posting => posting.RelationOrdinals.Length),
            relationDescriptors.Length + metadataDescriptors.Length + postingDescriptors.Length);
        return new CompactRetrievalIndexProjection(indexManifest, summary, counters);
    }

    public static string ComputeAnalysisRunId(FactualManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var identity = string.Join("\n", new[]
        {
            "retrieval-index-schema:2",
            $"tool-version:{manifest.ToolVersion}",
            $"analysis-requested:{manifest.Analysis.Requested}",
            $"analysis-effective:{manifest.Analysis.Effective}",
            $"trust:{manifest.Trust}",
            $"restore-performed:{manifest.RestorePerformed}",
        }
        .Concat(manifest.Extensions.Order(StringComparer.Ordinal).Select(static value => $"extension:{value}"))
        .Concat(manifest.Fragments.OrderBy(static value => value.FactId, StringComparer.Ordinal)
            .Select(static value => $"fragment:{value.FactId}|{value.Reference}|{value.Sha256}|{value.ByteLength}"))
        .Concat(manifest.Hashes.Order(StringComparer.Ordinal).Select(static value => $"hash:{value}")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static void ReadDocuments(
        FactualManifest manifest,
        IAggregateFileWriter files,
        CompactRetrievalIndexBuilder builder)
    {
        foreach (var fragment in manifest.Fragments
                     .Where(static value => IsFactReference(value.Reference, "document"))
                     .OrderBy(static value => value.Reference, StringComparer.Ordinal))
        {
            using var stream = OpenFragment(fragment, files);
            FactualFragmentScanner.ScanDocuments(stream, fragment.Reference, document =>
                builder.AddKnownDocument(new CompactDocumentMetadata(
                    RequiredString(document, "document_id"),
                    OptionalString(document, "project_id"),
                    RequiredString(document, "relative_path"),
                    OptionalBoolean(document, "generated_origin"))));
        }
    }

    private static ImmutableArray<ParsedRelation> ReadRelations(
        FactualManifest manifest,
        IAggregateFileWriter files,
        CompactRetrievalIndexBuilder builder)
    {
        var result = ImmutableArray.CreateBuilder<ParsedRelation>();
        string? previousId = null;
        foreach (var fragment in manifest.Fragments
                     .Where(static value => IsFactReference(value.Reference, "relation"))
                     .OrderBy(static value => value.Reference, StringComparer.Ordinal))
        {
            using var stream = OpenFragment(fragment, files);
            FactualFragmentScanner.ScanRelations(stream, fragment.Reference, relation =>
            {
                var parsed = ParseRelation(relation, fragment, manifest.ToolVersion, result.Count);
                if (previousId is not null && StringComparer.Ordinal.Compare(previousId, parsed.RelationId) >= 0)
                {
                    var reason = StringComparer.Ordinal.Equals(previousId, parsed.RelationId) ? "duplicate" : "decreasing";
                    throw new InvalidOperationException(
                        $"Compact retrieval relation id '{parsed.RelationId}' is {reason}; previous id is '{previousId}'.");
                }

                previousId = parsed.RelationId;
                result.Add(parsed);
                builder.AddRelation(parsed.ToObservation());
            });
        }

        return result.ToImmutable();
    }

    private static bool IsFactReference(string reference, string factType) =>
        reference.Contains($"/{factType}/", StringComparison.Ordinal) ||
        reference.Contains($"/{factType}s/", StringComparison.Ordinal);

    private static ParsedRelation ParseRelation(
        JsonElement relation,
        ManifestFragment fragment,
        string toolVersion,
        int ordinal)
    {
        var header = relation.GetProperty("header");
        var relationId = RequiredString(relation, "relation_id");
        var headerId = RequiredString(header, "id");
        if (!StringComparer.Ordinal.Equals(headerId, relationId))
        {
            throw new InvalidOperationException(
                $"Compact retrieval relation '{relationId}' does not match header id '{headerId}'.");
        }

        var details = ReadDetails(relation);
        var evidence = ReadEvidence(header);
        var origin = new CompactOriginMetadata(fragment.Reference, fragment.Sha256, GeneratorVersion(header) ?? toolVersion);
        return new ParsedRelation(
            ordinal,
            relationId,
            RequiredString(relation, "source_id"),
            OptionalString(relation, "target_id"),
            RequiredString(relation, "partition"),
            RequiredString(relation, "relation_kind"),
            RequiredString(header, "resolution"),
            RequiredString(relation, "resolution_method"),
            OptionalString(relation, "unresolved_reason"),
            details,
            ReadCandidates(relation),
            evidence,
            origin,
            Extensions(relation,
                ["header", "relation_id", "source_id", "target_id", "partition", "relation_kind", "resolution_method", "unresolved_reason", "details", "candidates"]));
    }

    private static ImmutableArray<ParsedEvidence> ReadEvidence(JsonElement header) =>
        header.GetProperty("evidence").EnumerateArray().Select(evidence => new ParsedEvidence(
            RequiredString(evidence, "document_id"),
            OptionalString(evidence, "project_id"),
            RequiredString(evidence, "relative_path"),
            RequiredInt32(evidence, "start_line"),
            RequiredInt32(evidence, "start_column"),
            RequiredInt32(evidence, "end_line"),
            RequiredInt32(evidence, "end_column"),
            OptionalBoolean(evidence, "generated_origin"),
            Extensions(evidence,
                ["document_id", "project_id", "relative_path", "start_line", "start_column", "end_line", "end_column", "generated_origin"])))
            .ToImmutableArray();

    private static ImmutableArray<RelationDetailJson>? ReadDetails(JsonElement relation) =>
        relation.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array
            ? details.EnumerateArray().Select(static detail =>
                new RelationDetailJson(RequiredString(detail, "key"), RequiredString(detail, "value"))).ToImmutableArray()
            : null;

    private static ImmutableArray<string>? ReadCandidates(JsonElement relation) =>
        relation.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array
            ? candidates.EnumerateArray().Select(static candidate => candidate.GetString()
                ?? throw new JsonException("Compact retrieval candidate must be a string.")).ToImmutableArray()
            : null;

    private ImmutableArray<RelationShardDescriptor> WriteRelationShards(
        ImmutableArray<byte[]> records,
        ImmutableArray<ParsedRelation> relations,
        string runId,
        IAggregateFileWriter files)
    {
        var packed = _shardWriter.Pack("relation", records, first => Prefix(runId, $"\"first_ordinal\":{first},"),
            first => relations[first].RelationId);
        return packed.Select((shard, index) =>
        {
            var path = $"raw/index/relations/{index:D4}.json";
            files.Write(path, shard.Bytes);
            return new RelationShardDescriptor(path, shard.FirstIndex, shard.LastIndex, shard.Count, shard.Bytes.Length);
        }).ToImmutableArray();
    }

    private ImmutableArray<MetadataShardDescriptor> WriteMetadataShards(
        CompactRetrievalIndexBuildResult built,
        string runId,
        IAggregateFileWriter files)
    {
        var result = ImmutableArray.CreateBuilder<MetadataShardDescriptor>();
        WriteKind("documents", built.Documents.Select(value => Serialize(value,
            CompactRetrievalIndexJsonContext.Default.CompactDocumentMetadata)).ToImmutableArray());
        WriteKind("origins", built.Origins.Select(value => Serialize(value,
            CompactRetrievalIndexJsonContext.Default.CompactOriginMetadata)).ToImmutableArray());
        return result.ToImmutable();

        void WriteKind(string kind, ImmutableArray<byte[]> records)
        {
            var packed = _shardWriter.Pack("metadata", records,
                first => Prefix(runId, $"\"kind\":\"{kind}\",\"first_ordinal\":{first},"),
                first => $"{kind}:{first}");
            foreach (var (shard, index) in packed.Select((value, index) => (value, index)))
            {
                var path = $"raw/index/metadata/{kind}-{index:D4}.json";
                files.Write(path, shard.Bytes);
                result.Add(new MetadataShardDescriptor(kind, path, shard.FirstIndex, shard.LastIndex, shard.Count, shard.Bytes.Length));
            }
        }
    }

    private ImmutableArray<PostingShardDescriptor> WritePostingShards(
        CompactPostingMaps maps,
        string runId,
        IAggregateFileWriter files)
    {
        var result = ImmutableArray.CreateBuilder<PostingShardDescriptor>();
        WriteFamily("project", maps.Projects);
        WriteFamily("source", maps.Sources);
        WriteFamily("target", maps.Targets);
        WriteFamily("kind", maps.Kinds);
        WriteFamily("resolution", maps.Resolutions);
        return result.ToImmutable();

        void WriteFamily(string family, ImmutableArray<CompactPostingList> postings)
        {
            var orderedPostings = postings
                .OrderBy(static value => Hash(value.Key), StringComparer.Ordinal)
                .ThenBy(static value => value.Key, StringComparer.Ordinal)
                .ToImmutableArray();
            var records = orderedPostings.Select(value => Serialize(value,
                CompactRetrievalIndexJsonContext.Default.CompactPostingList)).ToImmutableArray();
            var packed = _shardWriter.Pack("posting", records, _ => Prefix(runId, $"\"family\":\"{family}\","),
                first => orderedPostings[first].Key);
            foreach (var (shard, index) in packed.Select((value, index) => (value, index)))
            {
                var path = $"raw/index/postings/{family}/{index:D4}.json";
                files.Write(path, shard.Bytes);
                result.Add(new PostingShardDescriptor(
                    family,
                    path,
                    Hash(orderedPostings[shard.FirstIndex].Key),
                    Hash(orderedPostings[shard.LastIndex].Key),
                    shard.Count,
                    shard.Bytes.Length));
            }
        }
    }

    private static byte[] Prefix(string runId, string fields) =>
        Encoding.UTF8.GetBytes($"{{\"schema_version\":2,\"analysis_run_id\":\"{runId}\",{fields}\"entries\":[");

    private static void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo, IAggregateFileWriter files)
    {
        var serialized = Serialize(value, typeInfo);
        var bytes = new byte[serialized.Length + 1];
        serialized.CopyTo(bytes, 0);
        bytes[^1] = (byte)'\n';
        files.Write(path, bytes);
    }

    private static void WriteEntryPoints(ImmutableArray<string> entries, string runId, IAggregateFileWriter files)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", SchemaVersion);
            writer.WriteString("analysis_run_id", runId);
            writer.WriteString("kind", "entry-points");
            writer.WriteStartArray("entries");
            foreach (var entry in entries) writer.WriteStringValue(entry);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        files.Write(EntryPointsPath, bytes);
    }

    private static byte[] Serialize<T>(T value, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);

    private static Stream OpenFragment(ManifestFragment fragment, IAggregateFileWriter files)
    {
        var path = $"raw/{fragment.Reference}";
        if (!files.Exists(path))
        {
            throw new InvalidOperationException($"Persisted fragment is missing: {fragment.Reference}");
        }

        return files.OpenRead(path);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string RequiredString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new JsonException($"Compact retrieval input is missing required string '{name}'.");

    private static string? OptionalString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int RequiredInt32(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt32(out var result)
            ? result
            : throw new JsonException($"Compact retrieval input is missing required integer '{name}'.");

    private static bool OptionalBoolean(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;

    private static string? GeneratorVersion(JsonElement header) =>
        header.TryGetProperty("provenance", out var provenance) && provenance.ValueKind == JsonValueKind.Array
            ? provenance.EnumerateArray().Select(static value => OptionalString(value, "engine_version"))
                .FirstOrDefault(static value => value is not null)
            : null;

    private static JsonElement? Extensions(JsonElement value, IEnumerable<string> known)
    {
        var knownNames = known.ToHashSet(StringComparer.Ordinal);
        var properties = value.EnumerateObject().Where(property => !knownNames.Contains(property.Name))
            .OrderBy(static property => property.Name, StringComparer.Ordinal).ToArray();
        if (properties.Length == 0) return null;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in properties)
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static IEnumerable<CompactPostingList> AllPostingLists(CompactPostingMaps maps) =>
        maps.Projects.Concat(maps.Sources).Concat(maps.Targets).Concat(maps.Kinds).Concat(maps.Resolutions);

    private sealed record ParsedRelation(
        int Ordinal,
        string RelationId,
        string SourceId,
        string? TargetId,
        string Partition,
        string RelationKind,
        string Resolution,
        string ResolutionMethod,
        string? UnresolvedReason,
        ImmutableArray<RelationDetailJson>? Details,
        ImmutableArray<string>? Candidates,
        ImmutableArray<ParsedEvidence> Evidence,
        CompactOriginMetadata Origin,
        JsonElement? Extensions)
    {
        public CompactRelationObservation ToObservation() => new(
            Ordinal,
            SourceId,
            TargetId,
            Partition,
            RelationKind,
            Resolution,
            ResolutionMethod,
            UnresolvedReason,
            Details?.FirstOrDefault(static detail => detail.Key == "target_text")?.Value,
            Origin,
            Evidence.Select(static value => value.Metadata).ToImmutableArray());

        public CompactRelationRecord ToRecord(
            IReadOnlyDictionary<CompactOriginMetadata, int> originOrdinals,
            IReadOnlyDictionary<string, int> documentOrdinals) => new(
                RelationId,
                originOrdinals[Origin],
                SourceId,
                TargetId,
                Partition,
                RelationKind,
                Resolution,
                ResolutionMethod,
                UnresolvedReason,
                Details,
                Candidates,
                Evidence.Select(value => value.ToCompact(documentOrdinals)).ToImmutableArray(),
                Extensions);
    }

    private sealed record ParsedEvidence(
        string DocumentId,
        string? ProjectId,
        string RelativePath,
        int StartLine,
        int StartColumn,
        int EndLine,
        int EndColumn,
        bool GeneratedOrigin,
        JsonElement? Extensions)
    {
        public CompactDocumentMetadata Metadata => new(DocumentId, ProjectId, RelativePath, GeneratedOrigin);

        public CompactEvidence ToCompact(IReadOnlyDictionary<string, int> documentOrdinals) => new(
            documentOrdinals[DocumentId], StartLine, StartColumn, EndLine, EndColumn, Extensions);
    }

}

internal sealed record CompactRetrievalIndexProjection(
    CompactRetrievalIndexManifest Manifest,
    CompactIndexSummary Summary,
    CompactSerializationCounters Counters);
