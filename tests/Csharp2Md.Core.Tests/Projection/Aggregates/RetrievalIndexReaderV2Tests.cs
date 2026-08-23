using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class RetrievalIndexReaderV2Tests
{
    [Fact]
    public void Query_ReconstructsEveryLogicalFieldWithoutOpeningFacts()
    {
        var files = Project();
        files.Opened.Clear();

        var reader = RetrievalIndexReader.Open(files);
        var relation = Assert.Single(reader.Query(new RetrievalLookup("source", "source-1")));

        Assert.Equal("relation-1", relation.RelationId);
        Assert.Equal("facts/relation/test.json", relation.FragmentReference);
        Assert.Equal("source-1", relation.SourceId);
        Assert.Null(relation.TargetId);
        Assert.Equal("structural", relation.Partition);
        Assert.Equal("calls", relation.RelationKind);
        Assert.Equal("unresolved", relation.Resolution);
        Assert.Equal("unresolved", relation.ResolutionMethod);
        Assert.Equal("missing", relation.UnresolvedReason);
        Assert.Equal("Runtime.Target", relation.ObservedTargetText);
        Assert.Equal("Runtime.Target", Assert.Single(relation.Details!.Value).Value);
        Assert.Equal(["candidate-1"], relation.Candidates);
        Assert.Equal(7, relation.Extensions!.Value.GetProperty("future").GetInt32());
        var evidence = Assert.Single(relation.Evidence);
        Assert.Equal("document-1", evidence.DocumentId);
        Assert.Equal("Feature.cs", evidence.RelativePath);
        Assert.Equal(3, evidence.StartLine);
        Assert.False(evidence.GeneratedOrigin);
        Assert.Equal("hash-1", evidence.Extensions!.Value.GetProperty("future_evidence").GetString());
        Assert.DoesNotContain(files.Opened, static path => path.StartsWith("raw/facts/", StringComparison.Ordinal));
        Assert.Contains(files.Opened, static path => path.Contains("postings/source", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadUnknownGroups_ReconstructsCompleteRelations()
    {
        var reader = RetrievalIndexReader.Open(Project());

        var group = Assert.Single(reader.ReadUnknownGroups());

        Assert.Equal("missing", group.UnresolvedReason);
        Assert.Equal("source-1", group.SourceId);
        Assert.Equal("Runtime.Target", group.ObservedTargetText);
        Assert.Equal(1, group.Count);
        Assert.Equal(["relation-1"], group.RelationIds.ToArray());
        Assert.Equal("relation-1", Assert.Single(group.Relations!.Value).RelationId);
    }

    [Fact]
    public void Open_RejectsEverySchemaExceptTwoWithReceivedAndExpectedVersions()
    {
        var files = Project();
        var manifest = JsonNode.Parse(files.Contents["raw/index/manifest.json"])!.AsObject();
        manifest["schema_version"] = 1;
        files.Contents["raw/index/manifest.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());

        var exception = Assert.Throws<JsonException>(() => RetrievalIndexReader.Open(files));

        Assert.Contains("schema_version 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expected 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_RejectsWrongRunIdWithShardAndExpectedIdentity()
    {
        var files = Project();
        var manifest = JsonNode.Parse(files.Contents["raw/index/manifest.json"])!.AsObject();
        var postingPath = manifest["posting_shards"]!.AsArray()
            .Select(static value => value!.AsObject())
            .Single(static descriptor => descriptor["family"]!.GetValue<string>() == "source")["path"]!
            .GetValue<string>();
        var posting = JsonNode.Parse(files.Contents[postingPath])!.AsObject();
        posting["analysis_run_id"] = "other-run";
        files.Contents[postingPath] = Encoding.UTF8.GetBytes(posting.ToJsonString());
        var reader = RetrievalIndexReader.Open(files);

        var exception = Assert.Throws<JsonException>(() => reader.Query(new RetrievalLookup("source", "source-1")));

        Assert.Contains(postingPath, exception.Message, StringComparison.Ordinal);
        Assert.Contains("other-run", exception.Message, StringComparison.Ordinal);
        Assert.Contains(reader.Manifest.AnalysisRunId, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, "invalid relation ordinal -1")]
    [InlineData(0, "duplicate or invalid relation ordinal 0")]
    [InlineData(99, "missing relation ordinal 99")]
    public void Query_RejectsInvalidDuplicateOrMissingPostingOrdinals(int ordinal, string expected)
    {
        var files = Project();
        var postingPath = SourcePostingPath(files);
        var posting = JsonNode.Parse(files.Contents[postingPath])!.AsObject();
        posting["entries"]![0]!["relation_ordinals"] = ordinal == 0
            ? new JsonArray(0, 0)
            : new JsonArray(ordinal);
        files.Contents[postingPath] = Encoding.UTF8.GetBytes(posting.ToJsonString());
        var reader = RetrievalIndexReader.Open(files);

        var exception = Assert.Throws<JsonException>(() => reader.Query(new RetrievalLookup("source", "source-1")));

        Assert.Contains("family 'source', key 'source-1'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("document_ordinal", "document ordinal 99")]
    [InlineData("origin_ordinal", "origin ordinal 99")]
    public void Query_RejectsMissingDocumentOrOriginOrdinal(string field, string expected)
    {
        var files = Project();
        var manifest = JsonNode.Parse(files.Contents["raw/index/manifest.json"])!.AsObject();
        var relationPath = manifest["relation_shards"]![0]!["path"]!.GetValue<string>();
        var relationShard = JsonNode.Parse(files.Contents[relationPath])!.AsObject();
        if (field == "origin_ordinal")
        {
            relationShard["entries"]![0]![field] = 99;
        }
        else
        {
            relationShard["entries"]![0]!["evidence"]![0]![field] = 99;
        }

        files.Contents[relationPath] = Encoding.UTF8.GetBytes(relationShard.ToJsonString());
        var reader = RetrievalIndexReader.Open(files);

        var exception = Assert.Throws<JsonException>(() => reader.Query(new RetrievalLookup("source", "source-1")));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
        Assert.Contains(relationPath, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("relation")]
    [InlineData("metadata")]
    [InlineData("unknowns")]
    public void Reader_RejectsWrongRunIdInEveryResolvedArtifactKind(string artifact)
    {
        var files = Project();
        var manifest = JsonNode.Parse(files.Contents["raw/index/manifest.json"])!.AsObject();
        var path = artifact switch
        {
            "relation" => manifest["relation_shards"]![0]!["path"]!.GetValue<string>(),
            "metadata" => manifest["metadata_shards"]![0]!["path"]!.GetValue<string>(),
            _ => manifest["unknowns_path"]!.GetValue<string>(),
        };
        var json = JsonNode.Parse(files.Contents[path])!.AsObject();
        json["analysis_run_id"] = "other-run";
        files.Contents[path] = Encoding.UTF8.GetBytes(json.ToJsonString());
        var reader = RetrievalIndexReader.Open(files);

        var exception = artifact == "unknowns"
            ? Assert.Throws<JsonException>(() => reader.ReadUnknownGroups())
            : Assert.Throws<JsonException>(() => reader.Query(new RetrievalLookup("source", "source-1")));

        Assert.Contains(path, exception.Message, StringComparison.Ordinal);
        Assert.Contains("other-run", exception.Message, StringComparison.Ordinal);
        Assert.Contains(reader.Manifest.AnalysisRunId, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_CombinesSplitPostingShardsAndReturnsEachRelationOnceInOrdinalOrder()
    {
        var files = Project(twoRelations: true);
        var manifest = JsonNode.Parse(files.Contents["raw/index/manifest.json"])!.AsObject();
        var descriptors = manifest["posting_shards"]!.AsArray();
        var sourceDescriptor = descriptors.Select(static node => node!.AsObject())
            .Single(static descriptor => descriptor["family"]!.GetValue<string>() == "source");
        var originalPath = sourceDescriptor["path"]!.GetValue<string>();
        var secondPath = "raw/index/postings/source/split.json";
        var firstPosting = JsonNode.Parse(files.Contents[originalPath])!.AsObject();
        var secondPosting = firstPosting.DeepClone().AsObject();
        firstPosting["entries"]![0]!["relation_ordinals"] = new JsonArray(0);
        secondPosting["entries"]![0]!["relation_ordinals"] = new JsonArray(1);
        files.Contents[originalPath] = Encoding.UTF8.GetBytes(firstPosting.ToJsonString());
        files.Contents[secondPath] = Encoding.UTF8.GetBytes(secondPosting.ToJsonString());
        var secondDescriptor = sourceDescriptor.DeepClone().AsObject();
        secondDescriptor["path"] = secondPath;
        descriptors.Add(secondDescriptor);
        files.Contents["raw/index/manifest.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());

        var relations = RetrievalIndexReader.Open(files).Query(new RetrievalLookup("source", "source-1"));

        Assert.Equal(["relation-1", "relation-2"], relations.Select(static relation => relation.RelationId));
    }

    private static string SourcePostingPath(RecordingFiles files)
    {
        var manifest = JsonNode.Parse(files.Contents["raw/index/manifest.json"])!.AsObject();
        return manifest["posting_shards"]!.AsArray()
            .Select(static value => value!.AsObject())
            .Single(static descriptor => descriptor["family"]!.GetValue<string>() == "source")["path"]!
            .GetValue<string>();
    }

    private static RecordingFiles Project(bool twoRelations = false)
    {
        const string oneRelation = """
            {"schema_version":6,"documents":[],"relations":[{"header":{"id":"relation-1","resolution":"unresolved","provenance":[{"engine_version":"3.0.1"}],"evidence":[{"document_id":"document-1","relative_path":"Feature.cs","start_line":3,"start_column":2,"end_line":3,"end_column":8,"generated_origin":false,"future_evidence":"hash-1"}]},"relation_id":"relation-1","source_id":"source-1","partition":"structural","relation_kind":"calls","resolution_method":"unresolved","unresolved_reason":"missing","details":[{"key":"target_text","value":"Runtime.Target"}],"candidates":["candidate-1"],"future":7}]}
            """;
        const string twoRelation = """
            {"schema_version":6,"documents":[],"relations":[{"header":{"id":"relation-1","resolution":"unresolved","provenance":[{"engine_version":"3.0.1"}],"evidence":[{"document_id":"document-1","relative_path":"Feature.cs","start_line":3,"start_column":2,"end_line":3,"end_column":8,"generated_origin":false}]},"relation_id":"relation-1","source_id":"source-1","partition":"structural","relation_kind":"calls","resolution_method":"unresolved","unresolved_reason":"missing"},{"header":{"id":"relation-2","resolution":"exact","provenance":[{"engine_version":"3.0.1"}],"evidence":[{"document_id":"document-1","relative_path":"Feature.cs","start_line":4,"start_column":2,"end_line":4,"end_column":8,"generated_origin":false}]},"relation_id":"relation-2","source_id":"source-1","target_id":"target-2","partition":"structural","relation_kind":"calls","resolution_method":"exact"}]}
            """;
        var json = twoRelations ? twoRelation : oneRelation;
        var files = new RecordingFiles();
        var bytes = Encoding.UTF8.GetBytes(json);
        files.Write("raw/facts/relation/test.json", bytes);
        var fragment = new ManifestFragment(
            "relation-1",
            "facts/relation/test.json",
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes.Length);
        var manifest = new FactualManifest(
            6,
            "3.0.1",
            new ManifestAnalysis("full", "full"),
            "trusted",
            false,
            "none",
            [],
            new ManifestCoverage(1, 1, 1),
            [fragment],
            [fragment.Sha256]);
        new RetrievalIndexProjector().Project(manifest, files);
        return files;
    }

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public Dictionary<string, byte[]> Contents { get; } = new(StringComparer.Ordinal);
        public List<string> Opened { get; } = [];
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => Contents[relativePath] = bytes;
        public bool Exists(string relativePath) => Contents.ContainsKey(relativePath);
        public Stream OpenRead(string relativePath)
        {
            Opened.Add(relativePath);
            return new MemoryStream(Contents[relativePath], writable: false);
        }
    }
}
