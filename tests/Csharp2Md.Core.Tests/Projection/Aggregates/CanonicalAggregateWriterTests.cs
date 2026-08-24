using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Projection.Aggregates;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

[Trait("Category", "Integration")]
public sealed class CanonicalAggregateWriterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-aggregates-").FullName;
    private readonly string _input = Directory.CreateTempSubdirectory("csharp2md-aggregate-input-").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        Directory.Delete(_input, recursive: true);
    }

    [Fact]
    public void Write_CreatesExactV3SkeletonTree()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        var files = Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_root, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(ExpectedFiles, files);
        Assert.All(ExpectedDirectories, path => Assert.True(Directory.Exists(Path.Combine(_root, path))));
    }

    [Fact]
    public void Write_ManifestIsWrittenLast()
    {
        var recording = new RecordingFiles();

        new CanonicalAggregateWriter(recording).Write(_root, _input, false, Snapshot(), TimeProvider.System);

        Assert.Equal("raw/index/manifest.json", recording.Writes[^2]);
        Assert.Equal("raw/facts/manifest.json", recording.Writes[^1]);
    }

    [Fact]
    public void Write_OmitsV2DependenciesJson()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        Assert.False(File.Exists(Path.Combine(_root, "raw", "dependencies.json")));
        Assert.Equal("flowchart LR\n", File.ReadAllText(Path.Combine(_root, "raw", "dependencies.mmd")));
    }

    // COMP-06: a snapshot with no Graph still writes both empty-document contents, not an error and not a
    // stale value carried over from a previous run.
    [Fact]
    public void Write_ASnapshotWithNoGraph_StillWritesBothEmptyDocumentContents()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        Assert.Equal("flowchart LR\n", File.ReadAllText(Path.Combine(_root, "raw", "dependencies.mmd")));
        Assert.Equal("# Components\n", File.ReadAllText(Path.Combine(_root, "raw", "codebase", "components.md")));
    }

    // COMP-06/COMP-07: a snapshot carrying a real Graph writes exactly that Graph's Mermaid and
    // ComponentIndex, proving the writer reads AggregateOutputSnapshot.Graph and not a leftover fallback.
    [Fact]
    public void Write_ASnapshotWithAGraph_WritesTheGraphsMermaidAndComponentIndex()
    {
        var graph = new ComponentGraphProjection(
            [],
            "flowchart LR\n    node0[\"Acme.Orders\"]\n",
            "# Components\n\n## project\n\n- id: `id1:component;kind=project;owners=id1%3Aproject%3Bpath%3DAcme.Orders.csproj`\n",
            []);

        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot() with { Graph = graph }, TimeProvider.System);

        Assert.Equal(graph.Mermaid, File.ReadAllText(Path.Combine(_root, "raw", "dependencies.mmd")));
        Assert.Equal(graph.ComponentIndex, File.ReadAllText(Path.Combine(_root, "raw", "codebase", "components.md")));
    }

    // RRI-21: generated-origin metadata is an additive factual field, so the schema moves to version 6.
    [Fact]
    public void SchemaVersion_IsSix()
    {
        Assert.Equal(6, FactualJsonSerializer.SchemaVersion);
    }

    [Fact]
    public void Write_MachineFilesAreUtf8LfWithoutTimestamp()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        foreach (var path in Directory.EnumerateFiles(Path.Combine(_root, "raw"), "*", SearchOption.AllDirectories)
                     .Where(path => !path.EndsWith("log.md", StringComparison.Ordinal)))
        {
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.DoesNotContain('\r', text);
            Assert.DoesNotContain("generated_at", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Write_OnlyAuditLogContainsTimestamp()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));

        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), time);

        Assert.Contains("2026-08-17T12:00:00.0000000+00:00", File.ReadAllText(Path.Combine(_root, "raw", "log.md")), StringComparison.Ordinal);
        Assert.DoesNotContain("2026-08-17", File.ReadAllText(Path.Combine(_root, "raw", "facts", "manifest.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_IdenticalInputsDifferOnlyInLogWhenTimeChanges()
    {
        var other = Path.Combine(Path.GetTempPath(), $"csharp2md-aggregates-other-{Guid.NewGuid():N}");
        try
        {
            new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), new FixedTimeProvider(DateTimeOffset.UnixEpoch));
            new CanonicalAggregateWriter().Write(other, _input, false, Snapshot(), new FixedTimeProvider(DateTimeOffset.UnixEpoch.AddDays(1)));

            foreach (var relative in ExpectedFiles.Where(static path => path != "raw/log.md" && path != ".csharp2md-output"))
            {
                Assert.Equal(File.ReadAllBytes(Path.Combine(_root, relative)), File.ReadAllBytes(Path.Combine(other, relative)));
            }

            Assert.NotEqual(File.ReadAllBytes(Path.Combine(_root, "raw/log.md")), File.ReadAllBytes(Path.Combine(other, "raw/log.md")));
        }
        finally
        {
            if (Directory.Exists(other)) Directory.Delete(other, recursive: true);
        }
    }

    [Fact]
    public void Write_ManifestCarriesSecurityVersionCoverageAndIndexes()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "raw", "facts", "manifest.json")));
        var root = manifest.RootElement;

        Assert.Equal(2, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("3.0.0", root.GetProperty("tool_version").GetString());
        Assert.Equal("syntax-only", root.GetProperty("analysis").GetProperty("requested").GetString());
        Assert.Equal("untrusted", root.GetProperty("trust").GetString());
        Assert.False(root.GetProperty("restore_performed").GetBoolean());
        Assert.Equal("none", root.GetProperty("isolation").GetString());
        Assert.Equal(2, root.GetProperty("coverage").GetProperty("documents").GetInt32());
        Assert.Empty(root.GetProperty("fragments").EnumerateArray());
    }

    [Fact]
    public void Write_InvalidFragmentReferencePreventsManifest()
    {
        var invalid = Snapshot() with
        {
            Fragments = [new(default, Csharp2Md.Core.Facts.Identity.ArtifactReference.Parse("facts/document/00/" + new string('0', 64) + ".json"), new string('0', 64), 1)],
        };

        Assert.Throws<InvalidOperationException>(() =>
            new CanonicalAggregateWriter().Write(_root, _input, false, invalid, TimeProvider.System));
        Assert.False(File.Exists(Path.Combine(_root, "raw", "facts", "manifest.json")));
        Assert.False(File.Exists(Path.Combine(_root, "raw", "index", "manifest.json")));
    }

    [Fact]
    public void Write_ValidatedFragmentsProduceIndexWithoutRewritingCanonicalBytes()
    {
        var files = new RecordingFiles();
        var documentJson = """
            {"schema_version":6,"documents":[{"header":{"generated_origin":false},"document_id":"document-orders","project_id":"project-orders","relative_path":"Orders.cs"}],"relations":[]}
            """;
        var relationJson = """
            {"schema_version":6,"documents":[],"relations":[{"header":{"id":"relation-orders-status","resolution":"exact","provenance":[{"engine_version":"3.0.0"}],"evidence":[{"document_id":"document-orders","relative_path":"Orders.cs","start_line":1,"start_column":1,"end_line":1,"end_column":2,"generated_origin":false}]},"relation_id":"relation-orders-status","source_id":"writer","target_id":"Orders.Status","partition":"structural","relation_kind":"writes","resolution_method":"exact"}]}
            """;
        var documentBytes = Encoding.UTF8.GetBytes(documentJson);
        var relationBytes = Encoding.UTF8.GetBytes(relationJson);
        var documentReference = ArtifactReference.Parse("facts/document/00/" + new string('0', 64) + ".json");
        var relationReference = ArtifactReference.Parse("facts/relation/11/" + new string('1', 64) + ".json");
        files.Seed($"raw/{documentReference.Value}", documentBytes);
        files.Seed($"raw/{relationReference.Value}", relationBytes);
        var snapshot = Snapshot() with
        {
            Fragments =
            [
                new(FactIdGrammar.Create("document", [("name", "orders")]), documentReference,
                    Convert.ToHexStringLower(SHA256.HashData(documentBytes)), documentBytes.Length),
                new(FactIdGrammar.Create("relation", [("name", "orders-status")]), relationReference,
                    Convert.ToHexStringLower(SHA256.HashData(relationBytes)), relationBytes.Length),
            ],
        };

        new CanonicalAggregateWriter(files).Write(_root, _input, false, snapshot, TimeProvider.System);

        Assert.Equal(documentBytes, files.Contents[$"raw/{documentReference.Value}"]);
        Assert.Equal(relationBytes, files.Contents[$"raw/{relationReference.Value}"]);
        Assert.Contains("raw/index/manifest.json", files.Writes);
        Assert.Contains("raw/index/summary.json", files.Writes);
        Assert.Contains("raw/index/catalogues/entry-points.json", files.Writes);
        Assert.All(new[] { "entities", "events", "integrations", "high-centrality" },
            name => Assert.DoesNotContain($"raw/index/catalogues/{name}.json", files.Writes));
        using var indexManifest = JsonDocument.Parse(files.Contents["raw/index/manifest.json"]);
        Assert.Single(indexManifest.RootElement.GetProperty("relation_shards").EnumerateArray());
        Assert.Contains(indexManifest.RootElement.GetProperty("posting_shards").EnumerateArray(),
            shard => shard.GetProperty("family").GetString() == "target");
        Assert.Equal("raw/facts/manifest.json", files.Writes[^1]);
    }

    [Fact]
    public void Write_PreparesOwnedOutputOnceAndRemovesStaleFiles()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);
        File.WriteAllText(Path.Combine(_root, "stale.txt"), "stale");

        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        Assert.False(File.Exists(Path.Combine(_root, "stale.txt")));
        Assert.True(File.Exists(Path.Combine(_root, ".csharp2md-output")));
    }

    [Fact]
    public void Write_ReplacementRemovesSchema1CataloguesAndPerKeyShards()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);
        var legacyCatalogue = Path.Combine(_root, "raw", "index", "catalogues", "entities.json");
        var legacyShard = Path.Combine(_root, "raw", "index", "shards", "target", "raw-key", "0000.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyCatalogue)!);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyShard)!);
        File.WriteAllText(legacyCatalogue, "legacy");
        File.WriteAllText(legacyShard, "legacy");

        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        Assert.False(File.Exists(legacyCatalogue));
        Assert.False(Directory.Exists(Path.Combine(_root, "raw", "index", "shards")));
        Assert.True(File.Exists(Path.Combine(_root, "raw", "index", "manifest.json")));
    }

    [Fact]
    public Task Write_ManifestMatchesApprovedSnapshot()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);
        return Verifier.Verify(File.ReadAllText(Path.Combine(_root, "raw", "facts", "manifest.json")), "json").UseDirectory("snapshots");
    }

    [Fact]
    public void Write_ARunWithNoRelations_StillWritesResolutionJsonAtVersion1WithEveryCountZero()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        var path = Path.Combine(_root, "raw", "facts", "relations", "resolution.json");
        Assert.True(File.Exists(path));
        using var resolution = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, resolution.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("resolution", resolution.RootElement.GetProperty("kind").GetString());
        Assert.Equal(0, resolution.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(8, resolution.RootElement.GetProperty("by_partition").GetArrayLength());
    }

    [Fact]
    public void Write_ARunWithRelations_WritesResolutionJsonWithMatchingCountsAndLeavesPartitionEnvelopesAtVersion2()
    {
        var relation = new RelationFactJson(
            new FactHeaderJson("id1:relation;owner=x;kind=calls;claim=y;ordinal=1", "relation", "syntactic", [], [], []),
            "id1:relation;owner=x;kind=calls;claim=y;ordinal=1", "id1:syntactic-symbol;owner=x", null,
            "structural", "calls", null, null, "syntactic", null);
        var projection = new RelationProjectionResult(
            [.. Enum.GetValues<RelationPartition>().Select(partition =>
                new RelationPartitionProjection(partition, partition == RelationPartition.Structural ? [relation] : []))]);

        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot() with { Relations = projection }, TimeProvider.System);

        var structuralPath = Path.Combine(_root, "raw", "facts", "relations", "structural.json");
        using var structural = JsonDocument.Parse(File.ReadAllText(structuralPath));
        Assert.Equal(2, structural.RootElement.GetProperty("schema_version").GetInt32());

        using var resolution = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "raw", "facts", "relations", "resolution.json")));
        Assert.Equal(1, resolution.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, resolution.RootElement.GetProperty("by_method").GetProperty("syntactic").GetInt32());
        var structuralMetrics = resolution.RootElement.GetProperty("by_partition").EnumerateArray()
            .Single(entry => entry.GetProperty("partition").GetString() == "structural");
        Assert.Equal(1, structuralMetrics.GetProperty("total").GetInt32());
    }

    private static AggregateOutputSnapshot Snapshot() => new(
        "architecture", "system-design", "3.0.0", AnalysisMode.SyntaxOnly, AnalysisMode.SyntaxOnly,
        TrustMode.Untrusted, [], new ManifestCoverage(1, 1, 2), []);

    private static readonly string[] ExpectedFiles =
    [
        ".csharp2md-output", "raw/CLAUDE.md", "raw/codebase/components.md", "raw/dependencies.mmd",
        "raw/facts/coverage.json", "raw/facts/database.json",
        "raw/facts/diagnostics.json", "raw/facts/manifest.json", "raw/facts/relations/compile-time.json",
        "raw/facts/relations/data.json", "raw/facts/relations/dependency-injection.json", "raw/facts/relations/events.json",
        "raw/facts/relations/grpc.json", "raw/facts/relations/http.json", "raw/facts/relations/inheritance.json",
        "raw/facts/relations/resolution.json",
        "raw/facts/relations/structural.json", "raw/facts/solutions.json", "raw/index/catalogues/entry-points.json",
        "raw/index/manifest.json", "raw/index/summary.json", "raw/index/unknowns.json",
        "raw/log.md", "raw/topic.yaml",
    ];

    private static readonly string[] ExpectedDirectories =
        ["raw/facts/projects", "raw/facts/documents", "raw/facts/symbols", "raw/codebase", "raw/index", "raw/index/catalogues"];

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public List<string> Writes { get; } = [];
        public Dictionary<string, byte[]> Contents { get; } = new(StringComparer.Ordinal);
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes)
        {
            Writes.Add(relativePath);
            Contents[relativePath] = bytes;
        }

        public bool Exists(string relativePath) => Contents.ContainsKey(relativePath);
        public Stream OpenRead(string relativePath) => new MemoryStream(Contents[relativePath], writable: false);
        public void Seed(string relativePath, byte[] bytes) => Contents.Add(relativePath, bytes);
    }
}
