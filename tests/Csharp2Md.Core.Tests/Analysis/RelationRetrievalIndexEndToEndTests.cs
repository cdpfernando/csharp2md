using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// RRI-05 through RRI-13 against the persisted index from a real syntax-only run.
/// The retrieval assertions intentionally read the entity catalogue, index manifest and one target
/// shard. They never open a factual relation aggregate.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RelationRetrievalIndexEndToEndTests(RelationRetrievalIndexEndToEndFixture fixture)
    : IClassFixture<RelationRetrievalIndexEndToEndFixture>
{
    [Fact]
    public void RRI05_OrdersStatusWriters_AreResolvedFromEntitiesAndOneTargetShard()
    {
        var rawRoot = TopicLayout.RawRoot(fixture.OutputA);
        var statusId = DatabaseColumnFactId.Create(
            DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, "order_headers"),
            "order_status").Value;
        Assert.Contains(statusId, ReadCatalogue(rawRoot, "entities"));
        var manifest = Read(rawRoot, "raw/index/manifest.json");
        var target = Assert.Single(manifest.GetProperty("shards").EnumerateArray(), shard =>
            shard.GetProperty("family").GetString() == "target" && shard.GetProperty("key").GetString() == statusId);

        var shard = Read(rawRoot, target.GetProperty("path").GetString()!);
        var writers = shard.GetProperty("entries").EnumerateArray()
            .Where(entry => entry.GetProperty("relation_kind").GetString() == "writes-column")
            .ToArray();

        Assert.NotEmpty(writers);
        Assert.All(writers, writer =>
        {
            Assert.Equal(statusId, writer.GetProperty("target_id").GetString());
            Assert.NotEmpty(RequiredString(writer, "relation_id"));
            Assert.NotEmpty(RequiredString(writer, "fragment_reference"));
        });
    }

    [Fact]
    public void RRI06_RRI13_IdenticalRunsKeepRelationIdsAndAnalysisRunIdStable()
    {
        var rawA = TopicLayout.RawRoot(fixture.OutputA);
        var rawB = TopicLayout.RawRoot(fixture.OutputB);
        var manifestA = Read(rawA, "raw/index/manifest.json");
        var manifestB = Read(rawB, "raw/index/manifest.json");

        Assert.Equal(manifestA.GetProperty("analysis_run_id").GetString(), manifestB.GetProperty("analysis_run_id").GetString());
        Assert.Equal(SourceRelationIds(rawA), SourceRelationIds(rawB));
    }

    [Fact]
    public void RRI07_RRI08_RRI10_RRI11_RRI12_SyntaxOnlySummaryReportsQualityAndLimits()
    {
        var rawRoot = TopicLayout.RawRoot(fixture.OutputA);
        var manifest = Read(rawRoot, "raw/index/manifest.json");
        var summary = Read(rawRoot, "raw/index/summary.json");
        var entries = SourceEntries(rawRoot);

        Assert.Equal(1, manifest.GetProperty("schema_version").GetInt32());
        Assert.NotEmpty(RequiredString(manifest, "analysis_run_id"));
        foreach (var shardDescriptor in manifest.GetProperty("shards").EnumerateArray())
        {
            var shard = Read(rawRoot, RequiredString(shardDescriptor, "path"));
            Assert.Equal(1, shard.GetProperty("schema_version").GetInt32());
            Assert.Equal(RequiredString(manifest, "analysis_run_id"), RequiredString(shard, "analysis_run_id"));
        }
        Assert.Equal("syntax-only", summary.GetProperty("analysis").GetProperty("effective").GetString());
        Assert.Equal("untrusted", summary.GetProperty("trust").GetString());
        Assert.False(summary.GetProperty("restore_performed").GetBoolean());
        Assert.Equal(DistinctSymbolCount(entries), summary.GetProperty("indexed_symbol_count").GetInt32());
        Assert.Equal(0, summary.GetProperty("mapped_entry_point_count").GetInt32());
        Assert.Equal(new[] { "compile-time", "dependency-injection", "grpc" },
            summary.GetProperty("analysis_limitations").EnumerateArray().Select(static value => RequiredString(value)).ToArray());

        AssertMetrics(entries, summary.GetProperty("by_partition"), "partition");
        AssertMetrics(entries, summary.GetProperty("by_relation_kind"), "relation_kind");
    }

    [Fact]
    public void RRI09_EveryIndexedEvidenceCarriesVersionProjectFragmentAndCoordinates()
    {
        var entries = SourceEntries(TopicLayout.RawRoot(fixture.OutputA));

        Assert.NotEmpty(entries);
        foreach (var evidence in entries.SelectMany(static entry => entry.GetProperty("evidence").EnumerateArray()))
        {
            Assert.NotEmpty(RequiredString(evidence, "generator_version"));
            Assert.NotEmpty(RequiredString(evidence, "project_id"));
            Assert.NotEmpty(RequiredString(evidence, "document_id"));
            Assert.NotEmpty(RequiredString(evidence, "fragment_sha256"));
            Assert.True(evidence.GetProperty("start_line").GetInt32() > 0);
            Assert.True(evidence.GetProperty("start_column").GetInt32() > 0);
            Assert.True(evidence.GetProperty("end_line").GetInt32() > 0);
            Assert.True(evidence.GetProperty("end_column").GetInt32() > 0);
        }
    }

    [Fact]
    public void RRI18_UnknownRelationAndEvidenceFieldsSurviveSyntheticProjection()
    {
        var fixture = new SyntheticProjectionFixture(
            """
            {"header":{"resolution":"exact","provenance":[{"engine_version":"3.0.1"}],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":7,"start_column":3,"end_line":7,"end_column":19,"generated_origin":false,"future_evidence":"kept"}]},"relation_id":"relation-extension","source_id":"source-1","target_id":"target-1","partition":"structural","relation_kind":"writes","resolution_method":"symbol-index","future_relation":{"version":2}}
            """);

        var entry = Assert.Single(fixture.SourceEntries);

        Assert.Equal(2, entry.GetProperty("extensions").GetProperty("future_relation").GetProperty("version").GetInt32());
        Assert.Equal("kept", entry.GetProperty("evidence")[0].GetProperty("extensions").GetProperty("future_evidence").GetString());
    }

    [Fact]
    public void RRI19_SameSemanticRelationsAtDifferentLocationsRemainSeparateIndexEntries()
    {
        var fixture = new SyntheticProjectionFixture(
            """
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":7,"start_column":3,"end_line":7,"end_column":19,"generated_origin":false}]},"relation_id":"relation-first","source_id":"source-1","target_id":"target-1","partition":"structural","relation_kind":"writes","resolution_method":"symbol-index"},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":8,"start_column":3,"end_line":8,"end_column":19,"generated_origin":false}]},"relation_id":"relation-second","source_id":"source-1","target_id":"target-1","partition":"structural","relation_kind":"writes","resolution_method":"symbol-index"}
            """);

        Assert.Equal(["relation-first", "relation-second"], fixture.SourceEntries.Select(static entry => RequiredString(entry, "relation_id")).Order(StringComparer.Ordinal));
        Assert.Equal([7, 8], fixture.SourceEntries.Select(static entry => entry.GetProperty("evidence")[0].GetProperty("start_line").GetInt32()).Order());
    }

    [Fact]
    public void RRI14_RRI15_UnknownGroupsConsolidateRequiredKeysAndOrderByImpactWithStableTies()
    {
        var relations = """
            {"header":{"resolution":"unresolved","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":1,"start_column":1,"end_line":1,"end_column":2,"generated_origin":false}]},"relation_id":"relation-high-a","source_id":"source-high","partition":"structural","relation_kind":"calls","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Runtime.High"}]},
            {"header":{"resolution":"unresolved","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":2,"start_column":1,"end_line":2,"end_column":2,"generated_origin":false}]},"relation_id":"relation-high-b","source_id":"source-high","partition":"structural","relation_kind":"calls","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Runtime.High"}]},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":3,"start_column":1,"end_line":3,"end_column":2,"generated_origin":false}]},"relation_id":"relation-impact","source_id":"source-other","target_id":"source-high","partition":"structural","relation_kind":"calls","resolution_method":"symbol-index"},
            {"header":{"resolution":"unresolved","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":4,"start_column":1,"end_line":4,"end_column":2,"generated_origin":false}]},"relation_id":"relation-tie-a","source_id":"source-a","partition":"structural","relation_kind":"calls","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Runtime.A"}]},
            {"header":{"resolution":"unresolved","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Orders.cs","start_line":5,"start_column":1,"end_line":5,"end_column":2,"generated_origin":false}]},"relation_id":"relation-tie-b","source_id":"source-b","partition":"structural","relation_kind":"calls","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Runtime.B"}]}
            """;
        var first = new SyntheticProjectionFixture(relations);
        var second = new SyntheticProjectionFixture(relations);

        var unknowns = first.UnknownGroups;
        var high = Assert.Single(unknowns, group => RequiredString(group, "source_id") == "source-high");
        Assert.Equal("not-proved", RequiredString(high, "unresolved_reason"));
        Assert.Equal("Runtime.High", RequiredString(high, "observed_target_text"));
        Assert.Equal(2, high.GetProperty("count").GetInt32());
        Assert.Equal(3, high.GetProperty("impact").GetInt32());
        Assert.Equal(["relation-high-a", "relation-high-b"], high.GetProperty("relation_ids").EnumerateArray().Select(static id => RequiredString(id)));
        Assert.Equal(["source-high", "source-a", "source-b"], unknowns.Select(static group => RequiredString(group, "source_id")));
        Assert.Equal(
            unknowns.Select(static group => RequiredString(group, "source_id")),
            second.UnknownGroups.Select(static group => RequiredString(group, "source_id")));
    }

    [Fact]
    public void RRI15_ProvenEntryPointUnknownSortsBeforeHigherImpactGroups()
    {
        var fixture = new SyntheticProjectionFixture(
            """
            {"header":{"resolution":"unresolved","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Entry.cs","start_line":1,"start_column":1,"end_line":1,"end_column":2,"generated_origin":false}]},"relation_id":"relation-entry-unknown","source_id":"source-entry","partition":"structural","relation_kind":"calls","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Runtime.Entry"}]},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Entry.cs","start_line":2,"start_column":1,"end_line":2,"end_column":2,"generated_origin":false}]},"relation_id":"relation-entry-proof","source_id":"source-entry","partition":"http","relation_kind":"aspnet-entrypoint","resolution_method":"symbol-index"},
            {"header":{"resolution":"unresolved","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"High.cs","start_line":3,"start_column":1,"end_line":3,"end_column":2,"generated_origin":false}]},"relation_id":"relation-high-unknown","source_id":"source-high","partition":"structural","relation_kind":"calls","unresolved_reason":"not-proved","resolution_method":"observed","details":[{"key":"target_text","value":"Runtime.High"}]},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"High.cs","start_line":4,"start_column":1,"end_line":4,"end_column":2,"generated_origin":false}]},"relation_id":"relation-high-impact","source_id":"source-other","target_id":"source-high","partition":"structural","relation_kind":"calls","resolution_method":"symbol-index"},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"High.cs","start_line":5,"start_column":1,"end_line":5,"end_column":2,"generated_origin":false}]},"relation_id":"relation-high-impact-second","source_id":"source-other-second","target_id":"source-high","partition":"structural","relation_kind":"calls","resolution_method":"symbol-index"}
            """);

        Assert.Equal(["source-entry", "source-high"], fixture.UnknownGroups.Select(static group => RequiredString(group, "source_id")));
        Assert.True(fixture.UnknownGroups[0].GetProperty("has_proven_entry_point").GetBoolean());
        Assert.True(fixture.UnknownGroups[1].GetProperty("impact").GetInt32() > fixture.UnknownGroups[0].GetProperty("impact").GetInt32());
    }

    [Fact]
    public void RRI16_CataloguesContainOnlyPersistedFactEntries()
    {
        var fixture = new SyntheticProjectionFixture(
            """
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Api.cs","start_line":1,"start_column":1,"end_line":1,"end_column":2,"generated_origin":false}]},"relation_id":"relation-entry-proof","source_id":"source-entry","partition":"http","relation_kind":"aspnet-entrypoint","resolution_method":"symbol-index"},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Events.cs","start_line":2,"start_column":1,"end_line":2,"end_column":2,"generated_origin":false}]},"relation_id":"relation-event","source_id":"source-event","target_id":"target-event","partition":"events","relation_kind":"publishes","resolution_method":"symbol-index"},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Http.cs","start_line":3,"start_column":1,"end_line":3,"end_column":2,"generated_origin":false}]},"relation_id":"relation-integration","source_id":"source-http","target_id":"target-http","partition":"http","relation_kind":"http-call","resolution_method":"symbol-index"}
            """);

        Assert.Equal(["source-entry", "source-event", "source-http", "target-event", "target-http"], ReadCatalogue(fixture, "entities"));
        Assert.Equal(["relation-event"], ReadCatalogue(fixture, "events"));
        Assert.Equal(["relation-entry-proof", "relation-integration"], ReadCatalogue(fixture, "integrations"));
        Assert.Equal(["source-entry"], ReadCatalogue(fixture, "entry-points"));
        Assert.Equal(["source-entry", "source-event", "source-http", "target-event", "target-http"], ReadCatalogue(fixture, "high-centrality"));
    }

    [Fact]
    public void MultipleEvidenceDocumentsPreserveOrdinalProvenanceEntries()
    {
        var fixture = new SyntheticProjectionFixture(
            """
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-first","relative_path":"First.cs","start_line":1,"start_column":1,"end_line":1,"end_column":2,"generated_origin":false},{"document_id":"document-second","relative_path":"Second.cs","start_line":2,"start_column":1,"end_line":2,"end_column":2,"generated_origin":false}]},"relation_id":"relation-two-documents","source_id":"source-1","target_id":"target-1","partition":"structural","relation_kind":"writes","resolution_method":"symbol-index"}
            """);

        var evidence = Assert.Single(fixture.SourceEntries).GetProperty("evidence").EnumerateArray().ToArray();

        Assert.Equal(["document-first", "document-second"], evidence.Select(static item => RequiredString(item, "document_id")));
        Assert.Equal(["First.cs", "Second.cs"], evidence.Select(static item => RequiredString(item, "relative_path")));
    }

    [Fact]
    public void RRI20_RRI21_GeneratedAndOrdinaryOriginsRemainExplicitInFactsAndIndex()
    {
        var fixture = new SyntheticProjectionFixture(
            """
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Generated.cs","start_line":1,"start_column":1,"end_line":1,"end_column":2,"generated_origin":true}]},"relation_id":"relation-generated","source_id":"source-generated","target_id":"target-generated","partition":"structural","relation_kind":"writes","resolution_method":"symbol-index"},
            {"header":{"resolution":"exact","provenance":[],"evidence":[{"document_id":"document-1","relative_path":"Ordinary.cs","start_line":2,"start_column":1,"end_line":2,"end_column":2,"generated_origin":false}]},"relation_id":"relation-ordinary","source_id":"source-ordinary","target_id":"target-ordinary","partition":"structural","relation_kind":"writes","resolution_method":"symbol-index"}
            """);
        var factual = fixture.Relations;

        Assert.True(factual[0].GetProperty("header").GetProperty("evidence")[0].GetProperty("generated_origin").GetBoolean());
        Assert.False(factual[1].GetProperty("header").GetProperty("evidence")[0].GetProperty("generated_origin").GetBoolean());
        Assert.True(Entry(fixture.SourceEntries, "relation-generated").GetProperty("evidence")[0].GetProperty("generated_origin").GetBoolean());
        Assert.False(Entry(fixture.SourceEntries, "relation-ordinary").GetProperty("evidence")[0].GetProperty("generated_origin").GetBoolean());
    }

    private static string[] ReadCatalogue(string rawRoot, string name) =>
        Read(rawRoot, $"raw/index/catalogues/{name}.json").GetProperty("entries").EnumerateArray()
            .Select(static entry => RequiredString(entry)).ToArray();

    private static string[] ReadCatalogue(SyntheticProjectionFixture fixture, string name)
    {
        using var document = JsonDocument.Parse(fixture.Files.Read($"raw/index/catalogues/{name}.json"));
        return document.RootElement.GetProperty("entries").EnumerateArray().Select(static entry => RequiredString(entry)).ToArray();
    }

    private static string[] SourceRelationIds(string rawRoot) => SourceEntries(rawRoot)
        .Select(static entry => RequiredString(entry, "relation_id")).Order(StringComparer.Ordinal).ToArray();

    private static JsonElement[] SourceEntries(string rawRoot)
    {
        var manifest = Read(rawRoot, "raw/index/manifest.json");
        return manifest.GetProperty("shards").EnumerateArray()
            .Where(static shard => shard.GetProperty("family").GetString() == "source")
            .SelectMany(shard => Read(rawRoot, shard.GetProperty("path").GetString()!).GetProperty("entries").EnumerateArray())
            .Select(static entry => entry.Clone())
            .OrderBy(static entry => RequiredString(entry, "relation_id"), StringComparer.Ordinal)
            .ToArray();
    }

    private static int DistinctSymbolCount(IEnumerable<JsonElement> entries) => entries
        .SelectMany(static entry => new[]
        {
            entry.GetProperty("source_id").GetString(),
            entry.TryGetProperty("target_id", out var target) ? target.GetString() : null,
        })
        .Where(static id => id is not null)
        .Distinct(StringComparer.Ordinal)
        .Count();

    private static void AssertMetrics(JsonElement[] entries, JsonElement actualMetrics, string entryKey)
    {
        var expected = entries.GroupBy(entry => entry.GetProperty(entryKey).GetString()!, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group => new
            {
                Key = group.Key,
                Total = group.Count(),
                Exact = group.Count(entry => entry.GetProperty("resolution").GetString() == "exact"),
                Dynamic = group.Count(entry => entry.GetProperty("resolution").GetString() == "dynamic"),
                Unresolved = group.Count(entry => entry.GetProperty("resolution").GetString() == "unresolved"),
            })
            .ToArray();
        var actual = actualMetrics.EnumerateArray().ToArray();

        Assert.Equal(expected.Select(static metric => metric.Key), actual.Select(static metric => RequiredString(metric, "key")));
        foreach (var metric in expected)
        {
            var result = Assert.Single(actual, value => RequiredString(value, "key") == metric.Key);
            Assert.Equal(metric.Total, result.GetProperty("total").GetInt32());
            Assert.Equal(metric.Exact, result.GetProperty("exact").GetInt32());
            Assert.Equal(metric.Dynamic, result.GetProperty("dynamic").GetInt32());
            Assert.Equal(metric.Unresolved, result.GetProperty("unresolved").GetInt32());
            Assert.Equal(metric.Exact * 100 / metric.Total, result.GetProperty("exact_percent").GetInt32());
            Assert.Equal(metric.Dynamic * 100 / metric.Total, result.GetProperty("dynamic_percent").GetInt32());
            Assert.Equal(metric.Unresolved * 100 / metric.Total, result.GetProperty("unresolved_percent").GetInt32());
        }
    }

    private static JsonElement Read(string rawRoot, string relativePath)
    {
        var path = relativePath.StartsWith("raw/", StringComparison.Ordinal)
            ? relativePath["raw/".Length..]
            : relativePath;
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(rawRoot, path.Replace('/', Path.DirectorySeparatorChar))));
        return document.RootElement.Clone();
    }

    private static string RequiredString(JsonElement value, string property) =>
        RequiredString(value.GetProperty(property));

    private static string RequiredString(JsonElement value) =>
        value.GetString() ?? throw new InvalidOperationException("Expected a JSON string.");

    private static JsonElement Entry(IEnumerable<JsonElement> entries, string relationId) =>
        Assert.Single(entries, entry => RequiredString(entry, "relation_id") == relationId);

    private sealed class SyntheticProjectionFixture
    {
        public SyntheticProjectionFixture(string relations)
        {
            var document = """
                {"schema_version":6,"documents":[{"document_id":"document-1","project_id":"project-1"}],"relations":[
                """ + relations + "]}";
            var bytes = Encoding.UTF8.GetBytes(document);
            Files.Write("raw/facts/relations/synthetic.json", bytes);
            var fragment = new ManifestFragment(
                "fact-synthetic",
                "facts/relations/synthetic.json",
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

            Projection = new RetrievalIndexProjector().Project(manifest, Files);
            Relations = Read(Files.Read("raw/facts/relations/synthetic.json")).GetProperty("relations").EnumerateArray()
                .Select(static relation => relation.Clone()).ToArray();
            SourceEntries = Projection.Manifest.Shards.Where(static shard => shard.Family == "source")
                .SelectMany(shard => Read(Files.Read(shard.Path)).GetProperty("entries").EnumerateArray())
                .Select(static entry => entry.Clone()).ToArray();
            UnknownGroups = Read(Files.Read("raw/index/unknowns.json")).GetProperty("entries").EnumerateArray()
                .Select(static group => group.Clone()).ToArray();
        }

        public RecordingFiles Files { get; } = new();
        public RetrievalIndexProjection Projection { get; }
        public JsonElement[] Relations { get; }
        public JsonElement[] SourceEntries { get; }
        public JsonElement[] UnknownGroups { get; }

        private static JsonElement Read(byte[] bytes)
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.Clone();
        }
    }

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => _files[relativePath] = bytes;
        public bool Exists(string relativePath) => _files.ContainsKey(relativePath);
        public byte[] Read(string relativePath) => _files[relativePath];
    }
}

public sealed class RelationRetrievalIndexEndToEndFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string OutputA { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-rri-e2e-a-{Guid.NewGuid():N}");
    public string OutputB { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-rri-e2e-b-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        await RunAsync(OutputA);
        await RunAsync(OutputB);
    }

    public Task DisposeAsync()
    {
        foreach (var output in new[] { OutputA, OutputB })
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }

        return Task.CompletedTask;
    }

    private static async Task RunAsync(string output)
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-rri-e2e-manifest-").FullName;
        try
        {
            var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);
            var request = Assert.IsType<AnalysisRequest>(
                AnalysisRequest.Create(manifest, output, topic: "acme-retrieval-index-e2e", domain: "system-design").Request);
            var result = await new AnalysisEngine().AnalyzeAsync(request);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
            }
        }
        finally
        {
            Directory.Delete(manifestDirectory, recursive: true);
        }
    }
}
