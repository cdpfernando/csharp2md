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

    private static RecordingFiles Project()
    {
        const string json = """
            {"schema_version":6,"documents":[],"relations":[{"header":{"id":"relation-1","resolution":"unresolved","provenance":[{"engine_version":"3.0.1"}],"evidence":[{"document_id":"document-1","relative_path":"Feature.cs","start_line":3,"start_column":2,"end_line":3,"end_column":8,"generated_origin":false,"future_evidence":"hash-1"}]},"relation_id":"relation-1","source_id":"source-1","partition":"structural","relation_kind":"calls","resolution_method":"unresolved","unresolved_reason":"missing","details":[{"key":"target_text","value":"Runtime.Target"}],"candidates":["candidate-1"],"future":7}]}
            """;
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
