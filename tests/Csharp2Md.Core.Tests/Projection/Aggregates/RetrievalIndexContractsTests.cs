using System.Text.Json;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class RetrievalIndexContractsTests
{
    [Fact]
    public void Contracts_SerializeSnakeCaseFieldsWithoutComputedMembers()
    {
        var entry = new RetrievalRelationEntry(
            "relation-1", "facts/relations/one.json", "source-1", "target-1", "project-1", "structural", "calls",
            "exact", "exact", null, null,
            [new RetrievalEvidence("document-1", "Feature.cs", 1, 1, 1, 10, true, "project-1", "hash-1", "3.0.1", null)],
            null);
        var shard = new RetrievalShard(1, "run-1", "target", "target-1", [entry]);
        var manifest = new RetrievalIndexManifest(
            1, "run-1", new ManifestAnalysis("syntax-only", "syntax-only"), "untrusted", false,
            [new ShardDescriptor("target", "target-1", "shards/target/key/0000.json", 1, 128)]);

        using var shardJson = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(shard, AggregateJsonContext.Default.RetrievalShard));
        using var manifestJson = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(manifest, AggregateJsonContext.Default.RetrievalIndexManifest));
        var entryJson = shardJson.RootElement.GetProperty("entries")[0];

        Assert.Equal(1, shardJson.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("run-1", shardJson.RootElement.GetProperty("analysis_run_id").GetString());
        Assert.Equal("relation-1", entryJson.GetProperty("relation_id").GetString());
        Assert.True(entryJson.GetProperty("evidence")[0].GetProperty("generated_origin").GetBoolean());
        Assert.False(entryJson.TryGetProperty("computed", out _));
        Assert.Equal("target", manifestJson.RootElement.GetProperty("shards")[0].GetProperty("family").GetString());
    }

    [Fact]
    public void Summary_ContainsAnalysisMetricsAndUnknownGroups()
    {
        var summary = new RetrievalIndexSummary(
            1, "run-1", new ManifestAnalysis("syntax-only", "syntax-only"), "untrusted", false, 2, 0,
            [new RetrievalQualityMetric("structural", 2, 1, 0, 1, 50, 0, 50)],
            [new RetrievalQualityMetric("calls", 2, 1, 0, 1, 50, 0, 50)],
            [new RetrievalUnknownGroup("unresolved", "source-1", "Target", 2, false, 2, ["relation-1", "relation-2"])]);

        using var json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(summary, AggregateJsonContext.Default.RetrievalIndexSummary));

        Assert.Equal("syntax-only", json.RootElement.GetProperty("analysis").GetProperty("effective").GetString());
        Assert.Equal(50, json.RootElement.GetProperty("by_partition")[0].GetProperty("exact_percent").GetInt32());
        Assert.Equal("Target", json.RootElement.GetProperty("unknown_groups")[0].GetProperty("observed_target_text").GetString());
    }
}
