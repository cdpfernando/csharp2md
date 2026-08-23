using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed class RetrievalIndexProjector(BoundedShardWriter? shardWriter = null)
{
    private static readonly string[] LookupFamilies = ["project", "source", "target", "kind", "resolution"];
    private readonly BoundedShardWriter _shardWriter = shardWriter ?? new BoundedShardWriter();

    public RetrievalIndexProjection Project(FactualManifest manifest, IAggregateFileWriter files)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(files);

        var runId = ComputeAnalysisRunId(manifest);
        var documents = LocateDocuments(manifest, files);
        var entries = ReadRelations(manifest, files, documents);
        var shards = WriteShards(entries, runId, files);
        var summary = CreateSummary(entries, manifest, runId);

        WriteCatalogue("entities", entries.SelectMany(static entry => new[] { entry.SourceId, entry.TargetId })
            .Where(static id => id is not null).Select(static id => id!).Distinct(StringComparer.Ordinal), files);
        WriteCatalogue("events", entries.Where(static entry => entry.Partition == "events").Select(static entry => entry.RelationId), files);
        WriteCatalogue("integrations", entries.Where(static entry => entry.Partition is "http" or "grpc").Select(static entry => entry.RelationId), files);
        WriteCatalogue("entry-points", [], files);
        WriteCatalogue("high-centrality", HighCentrality(entries), files);
        Write("raw/index/unknowns.json", new RetrievalUnknownCatalogue(1, "unknowns", summary.UnknownGroups), files);

        var indexManifest = new RetrievalIndexManifest(1, runId, manifest.Analysis, manifest.Trust, manifest.RestorePerformed, shards);
        Write("raw/index/summary.json", summary, files);
        Write("raw/index/manifest.json", indexManifest, files);
        return new RetrievalIndexProjection(indexManifest, summary);
    }

    public static string ComputeAnalysisRunId(FactualManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var identity = string.Join("\n", new[]
        {
            "retrieval-index-schema:1",
            $"tool-version:{manifest.ToolVersion}",
            $"analysis-requested:{manifest.Analysis.Requested}",
            $"analysis-effective:{manifest.Analysis.Effective}",
            $"trust:{manifest.Trust}",
            $"restore-performed:{manifest.RestorePerformed}",
        }.Concat(manifest.Hashes.Order(StringComparer.Ordinal)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private ImmutableArray<ShardDescriptor> WriteShards(ImmutableArray<RetrievalRelationEntry> entries, string runId, IAggregateFileWriter files)
    {
        var descriptors = ImmutableArray.CreateBuilder<ShardDescriptor>();
        foreach (var family in LookupFamilies)
        {
            var keyed = entries.SelectMany(entry => KeysFor(family, entry).Select(key => (Key: key, Entry: entry)))
                .GroupBy(static item => item.Key, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal);
            foreach (var group in keyed)
            {
                descriptors.AddRange(_shardWriter.Write(family, group.Key, runId, group.Select(static item => item.Entry), files));
            }
        }

        return descriptors.ToImmutable();
    }

    private static IEnumerable<string> KeysFor(string family, RetrievalRelationEntry entry) => family switch
    {
        "project" when entry.ProjectId is not null => [entry.ProjectId],
        "source" => [entry.SourceId],
        "target" when entry.TargetId is not null => [entry.TargetId],
        "kind" => [entry.RelationKind],
        "resolution" => [entry.Resolution],
        _ => [],
    };

    private static Dictionary<string, string> LocateDocuments(FactualManifest manifest, IAggregateFileWriter files)
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fragment in manifest.Fragments.OrderBy(static fragment => fragment.Reference, StringComparer.Ordinal))
        {
            using var json = JsonDocument.Parse(ReadFragment(fragment, files));
            if (!json.RootElement.TryGetProperty("documents", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var document in values.EnumerateArray())
            {
                var documentId = String(document, "document_id");
                var projectId = String(document, "project_id");
                if (documentId is not null && projectId is not null)
                {
                    documents[documentId] = projectId;
                }
            }
        }

        return documents;
    }

    private static ImmutableArray<RetrievalRelationEntry> ReadRelations(
        FactualManifest manifest,
        IAggregateFileWriter files,
        IReadOnlyDictionary<string, string> documents)
    {
        var entries = ImmutableArray.CreateBuilder<RetrievalRelationEntry>();
        foreach (var fragment in manifest.Fragments.OrderBy(static fragment => fragment.Reference, StringComparer.Ordinal))
        {
            using var json = JsonDocument.Parse(ReadFragment(fragment, files));
            if (!json.RootElement.TryGetProperty("relations", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var relation in values.EnumerateArray())
            {
                entries.Add(ReadRelation(relation, fragment, manifest.ToolVersion, documents));
            }
        }

        return entries.OrderBy(static entry => entry.RelationId, StringComparer.Ordinal).ToImmutableArray();
    }

    private static RetrievalRelationEntry ReadRelation(
        JsonElement relation,
        ManifestFragment fragment,
        string toolVersion,
        IReadOnlyDictionary<string, string> documents)
    {
        var header = relation.GetProperty("header");
        var evidence = ReadEvidence(header, fragment, toolVersion, documents);
        return new RetrievalRelationEntry(
            Required(relation, "relation_id"),
            fragment.Reference,
            Required(relation, "source_id"),
            String(relation, "target_id"),
            evidence.FirstOrDefault(static item => item.ProjectId is not null)?.ProjectId,
            Required(relation, "partition"),
            Required(relation, "relation_kind"),
            Required(header, "resolution"),
            String(relation, "resolution_method") ?? "exact",
            String(relation, "unresolved_reason"),
            ObservedTargetText(relation),
            evidence,
            Extensions(relation, ["header", "relation_id", "source_id", "target_id", "partition", "relation_kind", "unresolved_reason", "resolution_method"]));
    }

    private static ImmutableArray<RetrievalEvidence> ReadEvidence(
        JsonElement header,
        ManifestFragment fragment,
        string toolVersion,
        IReadOnlyDictionary<string, string> documents)
    {
        if (!header.TryGetProperty("evidence", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var generatorVersion = GeneratorVersion(header) ?? toolVersion;
        return values.EnumerateArray().Select(evidence =>
        {
            var documentId = Required(evidence, "document_id");
            documents.TryGetValue(documentId, out var projectId);
            return new RetrievalEvidence(
                documentId,
                Required(evidence, "relative_path"),
                RequiredInt(evidence, "start_line"),
                RequiredInt(evidence, "start_column"),
                RequiredInt(evidence, "end_line"),
                RequiredInt(evidence, "end_column"),
                Bool(evidence, "generated_origin"),
                projectId,
                fragment.Sha256,
                generatorVersion,
                Extensions(evidence, ["document_id", "relative_path", "start_line", "start_column", "end_line", "end_column", "generated_origin"]));
        }).ToImmutableArray();
    }

    private static RetrievalIndexSummary CreateSummary(ImmutableArray<RetrievalRelationEntry> entries, FactualManifest manifest, string runId)
    {
        var unknown = entries.Where(static entry => entry.Resolution == "unresolved")
            .GroupBy(static entry => (entry.UnresolvedReason ?? "", entry.SourceId, entry.ObservedTargetText ?? ""))
            .Select(group => new RetrievalUnknownGroup(
                group.Key.Item1,
                group.Key.SourceId,
                group.Key.Item3,
                group.Count(),
                HasProvenEntryPoint: false,
                Impact: Impact(entries, group.Key.SourceId),
                group.Select(static entry => entry.RelationId).Order(StringComparer.Ordinal).ToImmutableArray()))
            .OrderByDescending(static group => group.HasProvenEntryPoint)
            .ThenByDescending(static group => group.Impact)
            .ThenBy(static group => group.UnresolvedReason, StringComparer.Ordinal)
            .ThenBy(static group => group.SourceId, StringComparer.Ordinal)
            .ThenBy(static group => group.ObservedTargetText, StringComparer.Ordinal)
            .ToImmutableArray();
        var limitations = manifest.Analysis.Effective == "syntax-only"
            ? ImmutableArray.Create("compile-time", "dependency-injection", "grpc")
            : [];
        return new RetrievalIndexSummary(
            1,
            runId,
            manifest.Analysis,
            manifest.Trust,
            manifest.RestorePerformed,
            entries.SelectMany(static entry => new[] { entry.SourceId, entry.TargetId }).Where(static id => id is not null).Distinct(StringComparer.Ordinal).Count(),
            0,
            Metrics(entries, static entry => entry.Partition),
            Metrics(entries, static entry => entry.RelationKind),
            unknown,
            limitations);
    }

    private static ImmutableArray<RetrievalQualityMetric> Metrics(ImmutableArray<RetrievalRelationEntry> entries, Func<RetrievalRelationEntry, string> key) =>
        entries.GroupBy(key).OrderBy(static group => group.Key, StringComparer.Ordinal).Select(group =>
        {
            var total = group.Count();
            var exact = group.Count(static entry => entry.Resolution == "exact");
            var dynamic = group.Count(static entry => entry.Resolution == "dynamic");
            var unresolved = group.Count(static entry => entry.Resolution == "unresolved");
            return new RetrievalQualityMetric(group.Key, total, exact, dynamic, unresolved,
                Percentage(exact, total), Percentage(dynamic, total), Percentage(unresolved, total));
        }).ToImmutableArray();

    private static IEnumerable<string> HighCentrality(ImmutableArray<RetrievalRelationEntry> entries) =>
        entries.SelectMany(static entry => new[] { entry.SourceId, entry.TargetId }).Where(static id => id is not null).Select(static id => id!)
            .GroupBy(static id => id, StringComparer.Ordinal).OrderByDescending(static group => group.Count()).ThenBy(static group => group.Key, StringComparer.Ordinal).Select(static group => group.Key);

    private static int Impact(ImmutableArray<RetrievalRelationEntry> entries, string symbolId) =>
        entries.Count(entry => entry.SourceId == symbolId || entry.TargetId == symbolId);

    private static string? ObservedTargetText(JsonElement relation)
    {
        if (!relation.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return details.EnumerateArray().Where(static detail => String(detail, "key") == "target_text")
            .Select(static detail => String(detail, "value")).FirstOrDefault(static value => value is not null);
    }

    private static string? GeneratorVersion(JsonElement header)
    {
        if (!header.TryGetProperty("provenance", out var provenance) || provenance.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return provenance.EnumerateArray().Select(static value => String(value, "engine_version")).FirstOrDefault(static value => value is not null);
    }

    private static JsonElement? Extensions(JsonElement value, IEnumerable<string> known)
    {
        var knownNames = known.ToHashSet(StringComparer.Ordinal);
        var properties = value.EnumerateObject().Where(property => !knownNames.Contains(property.Name)).OrderBy(static property => property.Name, StringComparer.Ordinal).ToArray();
        if (properties.Length == 0)
        {
            return null;
        }

        using var buffer = new MemoryStream();
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

        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }

    private static byte[] ReadFragment(ManifestFragment fragment, IAggregateFileWriter files)
    {
        var path = $"raw/{fragment.Reference}";
        if (!files.Exists(path))
        {
            throw new InvalidOperationException($"Persisted fragment is missing: {fragment.Reference}");
        }

        return files.Read(path);
    }

    private static void Write(string path, AggregateEnvelope value, IAggregateFileWriter files) =>
        files.Write(path, Serialize(value, AggregateJsonContext.Default.AggregateEnvelope));

    private static void Write(string path, RetrievalIndexSummary value, IAggregateFileWriter files) =>
        files.Write(path, Serialize(value, AggregateJsonContext.Default.RetrievalIndexSummary));

    private static void Write(string path, RetrievalIndexManifest value, IAggregateFileWriter files) =>
        files.Write(path, Serialize(value, AggregateJsonContext.Default.RetrievalIndexManifest));

    private static void Write(string path, RetrievalUnknownCatalogue value, IAggregateFileWriter files) =>
        files.Write(path, Serialize(value, AggregateJsonContext.Default.RetrievalUnknownCatalogue));

    private static void WriteCatalogue(string name, IEnumerable<string> entries, IAggregateFileWriter files) =>
        Write($"raw/index/catalogues/{name}.json", new AggregateEnvelope(1, name, entries.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray()), files);

    private static byte[] Serialize<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(JsonSerializer.Serialize(value, typeInfo).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");

    private static string Required(JsonElement value, string name) => String(value, name) ?? throw new InvalidOperationException($"Relation index input is missing '{name}'.");
    private static int RequiredInt(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.TryGetInt32(out var result) ? result : throw new InvalidOperationException($"Relation index input is missing '{name}'.");
    private static string? String(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Bool(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True;
    private static int Percentage(int value, int total) => total == 0 ? 0 : value * 100 / total;
}

internal sealed record RetrievalIndexProjection(RetrievalIndexManifest Manifest, RetrievalIndexSummary Summary);
