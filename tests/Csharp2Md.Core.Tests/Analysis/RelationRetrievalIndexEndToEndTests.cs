using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
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
        var summary = Read(rawRoot, "raw/index/summary.json");
        var entries = SourceEntries(rawRoot);

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

    private static string[] ReadCatalogue(string rawRoot, string name) =>
        Read(rawRoot, $"raw/index/catalogues/{name}.json").GetProperty("entries").EnumerateArray()
            .Select(static entry => RequiredString(entry)).ToArray();

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
