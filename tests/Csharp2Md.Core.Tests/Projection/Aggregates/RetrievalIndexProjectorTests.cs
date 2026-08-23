using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class RetrievalIndexProjectorTests
{
    [Fact]
    public void Project_WritesOneRelationPayloadAndOrdinalOnlyPostingsThenManifestLast()
    {
        var files = new RecordingFiles();
        var manifest = Manifest(files,
            ("facts/documents/orders.json", Documents()),
            ("facts/relations/all.json", Relations(Relation("relation-a", target: "target-a"), Relation("relation-b"))));

        var projection = new RetrievalIndexProjector().Project(manifest, files);

        Assert.Equal(2, projection.Manifest.SchemaVersion);
        Assert.Equal(2, projection.Counters.RelationRecords);
        Assert.Equal(2, projection.Counters.MetadataRecords);
        Assert.Equal(6, projection.Counters.PostingLists);
        Assert.Equal(9, projection.Counters.PostingOrdinals);
        Assert.Equal(8, projection.Counters.CompletedEnvelopes);
        Assert.Equal(5, projection.Manifest.PostingShards.Select(static value => value.Family).Distinct().Count());
        var relationEntries = projection.Manifest.RelationShards.SelectMany(descriptor =>
            Read(files, descriptor.Path).GetProperty("entries").EnumerateArray().Select(static value => value.Clone())).ToArray();
        Assert.Equal(["relation-a", "relation-b"], relationEntries.Select(static value => value.GetProperty("relation_id").GetString()));
        Assert.All(projection.Manifest.PostingShards, descriptor =>
        {
            var posting = Read(files, descriptor.Path);
            Assert.Equal(2, posting.GetProperty("schema_version").GetInt32());
            Assert.All(posting.GetProperty("entries").EnumerateArray(), static entry =>
                Assert.Equal(["key", "relation_ordinals"], entry.EnumerateObject().Select(static property => property.Name)));
        });
        Assert.All(files.Writes.Where(static pair => pair.Key.StartsWith("raw/index/", StringComparison.Ordinal)),
            static pair =>
            {
                Assert.Equal((byte)'\n', pair.Value[^1]);
                Assert.Equal(1, pair.Value.Count(static value => value == (byte)'\n'));
                _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(pair.Value);
            });
        Assert.Equal("raw/index/manifest.json", files.WriteOrder[^1]);
    }

    [Fact]
    public void Project_AddingOneRelationIncrementsOnlyItsLinearSerializationWork()
    {
        var firstFiles = new RecordingFiles();
        var first = new RetrievalIndexProjector().Project(Manifest(firstFiles,
            ("facts/documents/orders.json", Documents()),
            ("facts/relations/all.json", Relations(Relation("relation-a", target: "target-a")))), firstFiles);
        var secondFiles = new RecordingFiles();
        var second = new RetrievalIndexProjector().Project(Manifest(secondFiles,
            ("facts/documents/orders.json", Documents()),
            ("facts/relations/all.json", Relations(Relation("relation-a", target: "target-a"), Relation("relation-b")))),
            secondFiles);

        Assert.Equal(1, second.Counters.RelationRecords - first.Counters.RelationRecords);
        Assert.Equal(0, second.Counters.MetadataRecords - first.Counters.MetadataRecords);
        Assert.Equal(1, second.Counters.PostingLists - first.Counters.PostingLists);
        Assert.Equal(4, second.Counters.PostingOrdinals - first.Counters.PostingOrdinals);
    }

    [Fact]
    public void Project_NormalizesMetadataKnownFieldsExtensionsAndUnknowns()
    {
        var files = new RecordingFiles();
        var manifest = Manifest(files,
            ("facts/documents/orders.json", Documents()),
            ("facts/relations/all.json", Relations(Relation("relation-a", details: true, extension: true))));

        var projection = new RetrievalIndexProjector().Project(manifest, files);

        var relation = Read(files, Assert.Single(projection.Manifest.RelationShards).Path).GetProperty("entries")[0];
        Assert.Equal("Target.Text", relation.GetProperty("details")[0].GetProperty("value").GetString());
        Assert.False(relation.GetProperty("extensions").TryGetProperty("details", out _));
        Assert.Equal(2, relation.GetProperty("extensions").GetProperty("future_relation").GetInt32());
        Assert.Equal(0, relation.GetProperty("evidence")[0].GetProperty("document_ordinal").GetInt32());
        Assert.Equal("kept", relation.GetProperty("evidence")[0].GetProperty("extensions").GetProperty("future_evidence").GetString());
        var summary = Read(files, projection.Manifest.SummaryPath);
        Assert.Equal(1, summary.GetProperty("unknown_group_count").GetInt32());
        Assert.False(summary.TryGetProperty("entries", out _));
        Assert.Equal([0], Read(files, projection.Manifest.UnknownsPath).GetProperty("entries")[0]
            .GetProperty("relation_ordinals").EnumerateArray().Select(static value => value.GetInt32()));
        Assert.Equal(2, projection.Manifest.MetadataShards.Length);
    }

    [Fact]
    public void Project_EmptyInputPublishesOnlyValidZeroArtifacts()
    {
        var files = new RecordingFiles();
        var manifest = new FactualManifest(6, "3.0.0", new ManifestAnalysis("full", "full"), "trusted", false,
            "none", [], new ManifestCoverage(0, 0, 0), [], []);

        var projection = new RetrievalIndexProjector().Project(manifest, files);

        Assert.Empty(projection.Manifest.RelationShards);
        Assert.Empty(projection.Manifest.MetadataShards);
        Assert.Empty(projection.Manifest.PostingShards);
        Assert.Equal(0, projection.Summary.IndexedEndpointCount);
        Assert.Equal(0, projection.Summary.UnknownGroupCount);
        Assert.Empty(Read(files, projection.Manifest.UnknownsPath).GetProperty("entries").EnumerateArray());
        Assert.Empty(Read(files, projection.Manifest.EntryPointsPath).GetProperty("entries").EnumerateArray());
        Assert.Equal("raw/index/manifest.json", files.WriteOrder[^1]);
    }

    [Fact]
    public void ComputeAnalysisRunId_IsSchemaTwoBoundAndManifestOrderInvariant()
    {
        var files = new RecordingFiles();
        var manifest = Manifest(files,
            ("facts/documents/orders.json", Documents()),
            ("facts/relations/all.json", Relations(Relation("relation-a"))));
        var reordered = manifest with
        {
            Fragments = manifest.Fragments.Reverse().ToImmutableArray(),
            Hashes = manifest.Hashes.Reverse().ToImmutableArray(),
        };

        Assert.Equal(RetrievalIndexProjector.ComputeAnalysisRunId(manifest),
            RetrievalIndexProjector.ComputeAnalysisRunId(reordered));
        var schemaOneIdentity = string.Join("\n", new[]
        {
            "retrieval-index-schema:1",
            "tool-version:3.0.0",
            "analysis-requested:full",
            "analysis-effective:full",
            "trust:trusted",
            "restore-performed:false",
        }.Concat(manifest.Hashes.Order(StringComparer.Ordinal)));
        var schemaOneRunId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(schemaOneIdentity)));
        Assert.NotEqual(schemaOneRunId, RetrievalIndexProjector.ComputeAnalysisRunId(manifest));
    }

    [Fact]
    public void Project_FragmentPermutationsProduceIdenticalPathsAndBytes()
    {
        (string Reference, string Json)[] fragments =
        [
            ("facts/documents/orders.json", Documents()),
            ("facts/relations/all.json", Relations(Relation("relation-a", target: "target-a"), Relation("relation-b"))),
        ];
        var firstFiles = new RecordingFiles();
        var firstManifest = Manifest(firstFiles, fragments);
        var secondFiles = new RecordingFiles();
        foreach (var fragment in firstManifest.Fragments)
        {
            secondFiles.Write($"raw/{fragment.Reference}", firstFiles.Writes[$"raw/{fragment.Reference}"]);
        }

        var secondManifest = firstManifest with
        {
            Fragments = firstManifest.Fragments.Reverse().ToImmutableArray(),
            Hashes = firstManifest.Hashes.Reverse().ToImmutableArray(),
        };

        var first = new RetrievalIndexProjector().Project(firstManifest, firstFiles);
        var second = new RetrievalIndexProjector().Project(secondManifest, secondFiles);

        Assert.Equal(first.Manifest.AnalysisRunId, second.Manifest.AnalysisRunId);
        var firstIndex = firstFiles.Writes.Where(static pair => pair.Key.StartsWith("raw/index/", StringComparison.Ordinal))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal).ToArray();
        var secondIndex = secondFiles.Writes.Where(static pair => pair.Key.StartsWith("raw/index/", StringComparison.Ordinal))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal).ToArray();
        Assert.Equal(firstIndex.Select(static pair => pair.Key), secondIndex.Select(static pair => pair.Key));
        Assert.All(firstIndex.Zip(secondIndex), static pair => Assert.Equal(pair.First.Value, pair.Second.Value));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("decreasing")]
    [InlineData("header")]
    public void Project_RejectsAmbiguousRelationOrdinalBeforePublishingManifest(string scenario)
    {
        var files = new RecordingFiles();
        var relations = scenario switch
        {
            "duplicate" => Relations(Relation("relation-b"), Relation("relation-b")),
            "decreasing" => Relations(Relation("relation-b"), Relation("relation-a")),
            _ => Relations(Relation("relation-a", headerId: "relation-other")),
        };
        var manifest = Manifest(files, ("facts/relations/all.json", relations));

        var exception = Assert.ThrowsAny<Exception>(() => new RetrievalIndexProjector().Project(manifest, files));

        Assert.Contains(scenario == "header" ? "does not match" : scenario, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(files.Writes.ContainsKey("raw/index/manifest.json"));
        Assert.Equal(Encoding.UTF8.GetBytes(relations), files.Writes["raw/facts/relations/all.json"]);
    }

    [Fact]
    public void Project_DefaultWriterAccepts262144AndRejects262145ByteRelationShard()
    {
        var baselineFiles = new RecordingFiles();
        var baseline = new RetrievalIndexProjector().Project(Manifest(baselineFiles,
            ("facts/relations/all.json", Relations(Relation("relation-boundary", paddingLength: 0)))), baselineFiles);
        var paddingLength = BoundedUtf8ShardWriter.DefaultMaximumBytes - Assert.Single(baseline.Manifest.RelationShards).ByteLength;

        var exactFiles = new RecordingFiles();
        var exact = new RetrievalIndexProjector().Project(Manifest(exactFiles,
            ("facts/relations/all.json", Relations(Relation("relation-boundary", paddingLength: paddingLength)))), exactFiles);
        var overFiles = new RecordingFiles();
        var exception = Assert.Throws<InvalidOperationException>(() => new RetrievalIndexProjector().Project(Manifest(overFiles,
            ("facts/relations/all.json", Relations(Relation("relation-boundary", paddingLength: paddingLength + 1)))), overFiles));

        Assert.Equal(262144, Assert.Single(exact.Manifest.RelationShards).ByteLength);
        Assert.Equal("Compact retrieval relation record 'relation-boundary' exceeds 262144 bytes.", exception.Message);
        Assert.False(overFiles.Writes.ContainsKey("raw/index/manifest.json"));
    }

    [Fact]
    public void Project_DefaultWriterAccepts262144AndRejects262145ByteMetadataShard()
    {
        var baselineFiles = new RecordingFiles();
        var baseline = new RetrievalIndexProjector().Project(Manifest(baselineFiles,
            ("facts/documents/orders.json", Documents("A")),
            ("facts/relations/all.json", Relations(Relation("relation-boundary", evidencePath: "A")))), baselineFiles);
        var baselineDescriptor = Assert.Single(baseline.Manifest.MetadataShards, static shard => shard.Kind == "documents");
        var paddingLength = BoundedUtf8ShardWriter.DefaultMaximumBytes - baselineDescriptor.ByteLength;
        var exactPath = "A" + new string('x', paddingLength);

        var exactFiles = new RecordingFiles();
        var exact = new RetrievalIndexProjector().Project(Manifest(exactFiles,
            ("facts/documents/orders.json", Documents(exactPath)),
            ("facts/relations/all.json", Relations(Relation("relation-boundary", evidencePath: exactPath)))), exactFiles);
        var overPath = exactPath + "x";
        var overFiles = new RecordingFiles();
        var exception = Assert.Throws<InvalidOperationException>(() => new RetrievalIndexProjector().Project(Manifest(overFiles,
            ("facts/documents/orders.json", Documents(overPath)),
            ("facts/relations/all.json", Relations(Relation("relation-boundary", evidencePath: overPath)))), overFiles));

        Assert.Equal(262144, Assert.Single(exact.Manifest.MetadataShards, static shard => shard.Kind == "documents").ByteLength);
        Assert.Equal("Compact retrieval metadata record 'documents:0' exceeds 262144 bytes.", exception.Message);
        Assert.False(overFiles.Writes.ContainsKey("raw/index/manifest.json"));
    }

    [Fact]
    public void Project_DefaultWriterAccepts262144AndRejects262145BytePostingShard()
    {
        var (relationCount, keyPadding) = PostingBoundary();
        var exactKey = "calls" + new string('x', keyPadding);
        var exactFiles = new RecordingFiles();
        var exact = new RetrievalIndexProjector().Project(Manifest(exactFiles,
            ("facts/relations/all.json", ManyRelations(relationCount, exactKey))), exactFiles);
        var overKey = exactKey + "x";
        var overFiles = new RecordingFiles();
        var exception = Assert.Throws<InvalidOperationException>(() => new RetrievalIndexProjector().Project(Manifest(overFiles,
            ("facts/relations/all.json", ManyRelations(relationCount, overKey))), overFiles));

        Assert.Equal(262144, Assert.Single(exact.Manifest.PostingShards,
            static shard => shard.Family == "kind").ByteLength);
        Assert.Equal($"Compact retrieval posting record '{overKey}' exceeds 262144 bytes.", exception.Message);
        Assert.False(overFiles.Writes.ContainsKey("raw/index/manifest.json"));
    }

    [Fact]
    public void Project_DelimiterClosingAndLfOverflowStartsAnotherProductionShard()
    {
        var oneFiles = new RecordingFiles();
        var one = new RetrievalIndexProjector(new BoundedUtf8ShardWriter(int.MaxValue)).Project(Manifest(oneFiles,
            ("facts/relations/all.json", Relations(Relation("relation-a")))), oneFiles);
        var exactSingleLength = Assert.Single(one.Manifest.RelationShards).ByteLength;
        var files = new RecordingFiles();

        var projection = new RetrievalIndexProjector(new BoundedUtf8ShardWriter(exactSingleLength)).Project(Manifest(files,
            ("facts/relations/all.json", Relations(Relation("relation-a"), Relation("relation-b")))), files);

        Assert.Equal(2, projection.Manifest.RelationShards.Length);
        Assert.All(projection.Manifest.RelationShards, descriptor => Assert.Equal(exactSingleLength, descriptor.ByteLength));
        Assert.Equal([0, 1], projection.Manifest.RelationShards.Select(static descriptor => descriptor.FirstOrdinal));
    }

    private static string Documents(string relativePath = "A.cs") =>
        $$"""{"schema_version":6,"documents":[{"header":{},"document_id":"document-a","project_id":"project-a","relative_path":"{{relativePath}}","generated_origin":false}],"relations":[]}""";

    private static string Relations(params string[] relations) =>
        $$"""{"schema_version":6,"documents":[],"relations":[{{string.Join(',', relations)}}]}""";

    private static string Relation(
        string id,
        string? target = null,
        string? headerId = null,
        bool details = false,
        bool extension = false,
        int paddingLength = -1,
        string evidencePath = "A.cs")
    {
        var evidence = new JsonObject
        {
            ["document_id"] = "document-a",
            ["relative_path"] = evidencePath,
            ["start_line"] = 1,
            ["start_column"] = 1,
            ["end_line"] = 1,
            ["end_column"] = 2,
            ["generated_origin"] = false,
        };
        if (extension) evidence["future_evidence"] = "kept";
        var relation = new JsonObject
        {
            ["header"] = new JsonObject
            {
                ["id"] = headerId ?? id,
                ["resolution"] = target is null ? "unresolved" : "exact",
                ["provenance"] = new JsonArray(new JsonObject { ["engine_version"] = "3.1.0" }),
                ["evidence"] = new JsonArray(evidence),
            },
            ["relation_id"] = id,
            ["source_id"] = "source-a",
            ["partition"] = "structural",
            ["relation_kind"] = "calls",
            ["resolution_method"] = target is null ? "unresolved" : "exact",
        };
        if (target is not null) relation["target_id"] = target;
        if (target is null) relation["unresolved_reason"] = "missing";
        if (details) relation["details"] = new JsonArray(new JsonObject { ["key"] = "target_text", ["value"] = "Target.Text" });
        if (extension) relation["future_relation"] = 2;
        if (paddingLength >= 0) relation["padding"] = new string('x', paddingLength);
        return relation.ToJsonString();
    }

    private static string ManyRelations(int count, string relationKind) => Relations(Enumerable.Range(0, count)
        .Select(index => $$"""{"header":{"id":"relation-{{index:D5}}","resolution":"exact","provenance":[],"evidence":[]},"relation_id":"relation-{{index:D5}}","source_id":"source","partition":"structural","relation_kind":"{{relationKind}}","resolution_method":"exact"}""")
        .ToArray());

    private static (int RelationCount, int KeyPadding) PostingBoundary()
    {
        var low = 1;
        var high = 100000;
        while (low < high)
        {
            var middle = low + ((high - low + 1) / 2);
            if (PostingEnvelopeLength(middle, "resolution", "exact") <= BoundedUtf8ShardWriter.DefaultMaximumBytes) low = middle;
            else high = middle - 1;
        }

        return (low, BoundedUtf8ShardWriter.DefaultMaximumBytes - PostingEnvelopeLength(low, "kind", "calls"));
    }

    private static int PostingEnvelopeLength(int relationCount, string family, string key)
    {
        var record = JsonSerializer.SerializeToUtf8Bytes(
            new CompactPostingList(key, Enumerable.Range(0, relationCount).ToImmutableArray()),
            CompactRetrievalIndexJsonContext.Default.CompactPostingList);
        var prefix = Encoding.UTF8.GetByteCount(
            $"{{\"schema_version\":2,\"analysis_run_id\":\"{new string('0', 64)}\",\"family\":\"{family}\",\"entries\":[");
        return prefix + record.Length + 3;
    }

    private static FactualManifest Manifest(RecordingFiles files, params (string Reference, string Json)[] fragments)
    {
        var manifestFragments = fragments.Select((fragment, index) =>
        {
            var bytes = Encoding.UTF8.GetBytes(fragment.Json);
            files.Write($"raw/{fragment.Reference}", bytes);
            return new ManifestFragment($"fact-{index}", fragment.Reference,
                Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
        }).ToImmutableArray();
        return new FactualManifest(6, "3.0.0", new ManifestAnalysis("full", "full"), "trusted", false,
            "none", [], new ManifestCoverage(1, 1, 1), manifestFragments,
            manifestFragments.Select(static fragment => fragment.Sha256).Order(StringComparer.Ordinal).ToImmutableArray());
    }

    private static JsonElement Read(RecordingFiles files, string path)
    {
        using var document = JsonDocument.Parse(files.Writes[path]);
        return document.RootElement.Clone();
    }

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public Dictionary<string, byte[]> Writes { get; } = new(StringComparer.Ordinal);
        public List<string> WriteOrder { get; } = [];
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes)
        {
            Writes[relativePath] = bytes;
            WriteOrder.Add(relativePath);
        }
        public bool Exists(string relativePath) => Writes.ContainsKey(relativePath);
        public Stream OpenRead(string relativePath) => new MemoryStream(Writes[relativePath], writable: false);
    }
}
