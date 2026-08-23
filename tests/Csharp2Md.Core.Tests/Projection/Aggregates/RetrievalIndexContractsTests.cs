using System.Text.Json;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class RetrievalIndexContractsTests
{
    [Fact]
    public void FrozenSchema1Context_ReadsPriorShardAndSummaryWithoutAdditiveFields()
    {
        var manifest = JsonSerializer.Deserialize(
            """
            {"schema_version":1,"analysis_run_id":"run-prior","analysis":{"requested":"full","effective":"full"},"trust":"trusted","restore_performed":true,"shards":[{"family":"target","key":"target-1","path":"shards/target/key/0000.json","entry_count":1,"byte_length":512}]}
            """, Schema1JsonContext.Default.RetrievalIndexManifest)!;
        var shard = JsonSerializer.Deserialize(
            """
            {"schema_version":1,"analysis_run_id":"run-prior","family":"target","key":"target-1","entries":[{"relation_id":"relation-prior","fragment_reference":"facts/relations/prior.json","source_id":"source-1","target_id":"target-1","project_id":"project-1","partition":"structural","relation_kind":"calls","resolution":"exact","resolution_method":"symbol-index","evidence":[{"document_id":"document-1","relative_path":"Prior.cs","start_line":1,"start_column":1,"end_line":1,"end_column":5,"project_id":"project-1","fragment_sha256":"hash-1","generator_version":"3.0.0"}]}]}
            """, Schema1JsonContext.Default.RetrievalShard)!;
        var summary = JsonSerializer.Deserialize(
            """
            {"schema_version":1,"analysis_run_id":"run-prior","analysis":{"requested":"full","effective":"full"},"trust":"trusted","restore_performed":true,"indexed_symbol_count":1,"mapped_entry_point_count":0,"by_partition":[],"by_relation_kind":[],"unknown_groups":[]}
            """, Schema1JsonContext.Default.RetrievalIndexSummary)!;

        Assert.Equal("shards/target/key/0000.json", Assert.Single(manifest.Shards).Path);
        Assert.Equal("run-prior", shard.AnalysisRunId);
        Assert.Equal("relation-prior", Assert.Single(shard.Entries).RelationId);
        Assert.False(shard.Entries[0].Evidence[0].GeneratedOrigin);
        Assert.Equal(1, summary.IndexedSymbolCount);
        Assert.True(summary.RestorePerformed);
        Assert.Null(summary.AnalysisLimitations);
    }

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

        using var shardJson = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(shard, Schema1JsonContext.Default.RetrievalShard));
        using var manifestJson = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(manifest, Schema1JsonContext.Default.RetrievalIndexManifest));
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

        using var json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(summary, Schema1JsonContext.Default.RetrievalIndexSummary));

        Assert.Equal("syntax-only", json.RootElement.GetProperty("analysis").GetProperty("effective").GetString());
        Assert.Equal(50, json.RootElement.GetProperty("by_partition")[0].GetProperty("exact_percent").GetInt32());
        Assert.Equal("Target", json.RootElement.GetProperty("unknown_groups")[0].GetProperty("observed_target_text").GetString());
    }
}
