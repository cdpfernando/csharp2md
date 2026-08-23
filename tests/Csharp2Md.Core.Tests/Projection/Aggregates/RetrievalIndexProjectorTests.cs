using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class RetrievalIndexProjectorTests
{
    [Fact]
    public void Project_StreamsFragmentsIntoDistinctCompatibleEntriesAndAllLookupFamilies()
    {
        var files = new RecordingFiles();
        var documents = """
            {"schema_version":6,"documents":[{"document_id":"document-orders","project_id":"project-orders","relative_path":"Orders.cs"}],"relations":[]}
            """;
        var relations = """
            {"schema_version":6,"documents":[],"relations":[
              {"header":{"resolution":"exact","provenance":[{"engine_version":"3.1.0"}],"evidence":[{"document_id":"document-orders","relative_path":"Orders.cs","start_line":7,"start_column":3,"end_line":7,"end_column":19,"generated_origin":true,"future_evidence":"kept"}]},"relation_id":"relation-a","source_id":"writer","target_id":"Orders.Status","partition":"structural","relation_kind":"writes","resolution_method":"symbol-index","future_relation":{"version":2}},
              {"header":{"resolution":"unresolved","provenance":[{"engine_version":"3.1.0"}],"evidence":[{"document_id":"document-orders","relative_path":"Orders.cs","start_line":8,"start_column":3,"end_line":8,"end_column":19,"generated_origin":false}]},"relation_id":"relation-b","source_id":"writer","partition":"structural","relation_kind":"writes","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Orders.Status"}]},
              {"header":{"resolution":"unresolved","provenance":[{"engine_version":"3.1.0"}],"evidence":[{"document_id":"document-orders","relative_path":"Orders.cs","start_line":9,"start_column":3,"end_line":9,"end_column":19,"generated_origin":false}]},"relation_id":"relation-c","source_id":"writer","partition":"structural","relation_kind":"writes","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Orders.Status"}]}
            ]}
            """;
        var manifest = Manifest(files, ("facts/documents/orders.json", documents), ("facts/relations/all.json", relations));

        var projection = new RetrievalIndexProjector().Project(manifest, files);

        Assert.Equal(projection.Manifest.AnalysisRunId, Read(files, "raw/index/manifest.json").GetProperty("analysis_run_id").GetString());
        Assert.Equal(projection.Manifest.AnalysisRunId, RetrievalIndexProjector.ComputeAnalysisRunId(manifest));
        Assert.Equal(
            ["project", "source", "target", "kind", "resolution"],
            projection.Manifest.Shards.Select(static shard => shard.Family).Distinct(StringComparer.Ordinal));
        var target = projection.Manifest.Shards.Single(static shard => shard.Family == "target" && shard.Key == "Orders.Status");
        var entry = Read(files, target.Path).GetProperty("entries")[0];
        Assert.Equal("relation-a", entry.GetProperty("relation_id").GetString());
        Assert.Equal("project-orders", entry.GetProperty("project_id").GetString());
        Assert.True(entry.GetProperty("evidence")[0].GetProperty("generated_origin").GetBoolean());
        Assert.Equal("3.1.0", entry.GetProperty("evidence")[0].GetProperty("generator_version").GetString());
        Assert.Equal(manifest.Fragments[1].Sha256, entry.GetProperty("evidence")[0].GetProperty("fragment_sha256").GetString());
        Assert.Equal(2, entry.GetProperty("extensions").GetProperty("future_relation").GetProperty("version").GetInt32());
        Assert.Equal("kept", entry.GetProperty("evidence")[0].GetProperty("extensions").GetProperty("future_evidence").GetString());
        Assert.Equal(new[] { "relation-a", "relation-b", "relation-c" }, Read(files, projection.Manifest.Shards.Single(static shard => shard.Family == "source").Path)
            .GetProperty("entries").EnumerateArray().Select(static item => item.GetProperty("relation_id").GetString()).ToArray());
        var missingTargetEntry = Read(files, projection.Manifest.Shards.Single(static shard => shard.Family == "source").Path)
            .GetProperty("entries").EnumerateArray().Single(static item => item.GetProperty("relation_id").GetString() == "relation-b");
        Assert.False(missingTargetEntry.TryGetProperty("target_id", out _));
        Assert.False(projection.Manifest.Shards.Any(static shard => shard.Family == "target" && shard.Key == ""));
        var unknown = Read(files, "raw/index/unknowns.json").GetProperty("entries")[0];
        Assert.Equal(2, unknown.GetProperty("count").GetInt32());
        Assert.Equal(new[] { "relation-b", "relation-c" }, unknown.GetProperty("relation_ids").EnumerateArray().Select(static id => id.GetString()).ToArray());
    }

    [Fact]
    public void Project_EmitsZeroSyntaxOnlySummaryAndDoesNotInventMissingEvidenceProject()
    {
        var files = new RecordingFiles();
        var relations = """
            {"schema_version":6,"documents":[],"relations":[{"header":{"resolution":"unresolved","provenance":[],"evidence":[{"document_id":"document-missing","relative_path":"Missing.cs","start_line":1,"start_column":1,"end_line":1,"end_column":2,"generated_origin":false}]},"relation_id":"relation-no-evidence","source_id":"source","partition":"grpc","relation_kind":"calls","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Remote"}]}]}
            """;
        var manifest = Manifest(files, ("facts/relations/all.json", relations)) with
        {
            Analysis = new ManifestAnalysis("syntax-only", "syntax-only"),
        };

        var projection = new RetrievalIndexProjector().Project(manifest, files);

        Assert.Empty(projection.Manifest.Shards.Where(static shard => shard.Family == "project"));
        var summary = Read(files, "raw/index/summary.json");
        Assert.Equal(1, summary.GetProperty("indexed_symbol_count").GetInt32());
        Assert.Equal(0, summary.GetProperty("mapped_entry_point_count").GetInt32());
        Assert.Contains("dependency-injection", summary.GetProperty("analysis_limitations").EnumerateArray().Select(static value => value.GetString()));
        Assert.Contains("grpc", summary.GetProperty("analysis_limitations").EnumerateArray().Select(static value => value.GetString()));
        Assert.Contains("compile-time", summary.GetProperty("analysis_limitations").EnumerateArray().Select(static value => value.GetString()));
        Assert.Equal(1, summary.GetProperty("by_partition")[0].GetProperty("unresolved").GetInt32());
        var missingProjectEvidence = Read(files, projection.Manifest.Shards.Single(static shard => shard.Family == "source").Path)
            .GetProperty("entries")[0].GetProperty("evidence")[0];
        Assert.False(missingProjectEvidence.TryGetProperty("project_id", out _));
        Assert.Equal("3.0.0", missingProjectEvidence.GetProperty("generator_version").GetString());
        Assert.All(new[] { "entities", "events", "integrations", "entry-points", "high-centrality" },
            name => Assert.True(files.Writes.ContainsKey($"raw/index/catalogues/{name}.json")));
        Assert.Empty(Read(files, "raw/index/catalogues/entry-points.json").GetProperty("entries").EnumerateArray());
    }

    [Fact]
    public void Project_EmptyInputPublishesValidZeroOutput()
    {
        var files = new RecordingFiles();
        var manifest = new FactualManifest(6, "3.0.0", new ManifestAnalysis("full", "full"), "trusted", false, "none", [], new ManifestCoverage(0, 0, 0), [], []);

        var projection = new RetrievalIndexProjector().Project(manifest, files);

        Assert.Empty(projection.Manifest.Shards);
        var summary = Read(files, "raw/index/summary.json");
        Assert.Equal(0, summary.GetProperty("indexed_symbol_count").GetInt32());
        Assert.Equal(0, summary.GetProperty("mapped_entry_point_count").GetInt32());
        Assert.Empty(summary.GetProperty("by_partition").EnumerateArray());
        Assert.Empty(Read(files, "raw/index/unknowns.json").GetProperty("entries").EnumerateArray());
        Assert.Equal(projection.Manifest.AnalysisRunId, Read(files, "raw/index/manifest.json").GetProperty("analysis_run_id").GetString());
        Assert.All(new[] { "entities", "events", "integrations", "entry-points", "high-centrality" },
            name => Assert.Empty(Read(files, $"raw/index/catalogues/{name}.json").GetProperty("entries").EnumerateArray()));
    }

    private static FactualManifest Manifest(RecordingFiles files, params (string Reference, string Json)[] fragments)
    {
        var manifestFragments = fragments.Select((fragment, index) =>
        {
            var bytes = Encoding.UTF8.GetBytes(fragment.Json);
            files.Write($"raw/{fragment.Reference}", bytes);
            return new ManifestFragment($"fact-{index}", fragment.Reference, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
        }).ToImmutableArray();

        return new FactualManifest(6, "3.0.0", new ManifestAnalysis("full", "full"), "trusted", false, "none", [], new ManifestCoverage(1, 1, 1), manifestFragments,
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
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => Writes[relativePath] = bytes;
        public bool Exists(string relativePath) => Writes.ContainsKey(relativePath);
        public byte[] Read(string relativePath) => Writes[relativePath];
    }
}
