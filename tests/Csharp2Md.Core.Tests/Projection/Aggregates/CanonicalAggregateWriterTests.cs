using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
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

        Assert.Equal("raw/facts/manifest.json", recording.Writes[^1]);
    }

    [Fact]
    public void Write_OmitsV2DependenciesJson()
    {
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(), TimeProvider.System);

        Assert.False(File.Exists(Path.Combine(_root, "raw", "dependencies.json")));
        Assert.Equal("flowchart LR\n", File.ReadAllText(Path.Combine(_root, "raw", "dependencies.mmd")));
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
        "raw/facts/relations/structural.json", "raw/facts/solutions.json", "raw/log.md", "raw/topic.yaml",
    ];

    private static readonly string[] ExpectedDirectories =
        ["raw/facts/projects", "raw/facts/documents", "raw/facts/symbols", "raw/codebase"];

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public List<string> Writes { get; } = [];
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => Writes.Add(relativePath);
        public bool Exists(string relativePath) => false;
        public byte[] Read(string relativePath) => throw new NotSupportedException();
    }
}
