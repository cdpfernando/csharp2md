using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

/// <summary>
/// Phase 0 of Database Access Discovery: the aggregate relation partitions written by a real
/// <see cref="AnalysisEngine.AnalyzeAsync"/> run over <c>fixtures/SyntheticSolution</c>. These assert on
/// the files the tool actually emits, which is where the two wiring defects (an unprojected
/// <c>RelationProjector</c> and a partition list missing <c>structural</c>) were originally observed.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AggregateRelationPartitionTests(AggregateRelationPartitionFixture fixture)
    : IClassFixture<AggregateRelationPartitionFixture>
{
    [Fact]
    public void HttpPartitionFile_CarriesTheHttpRelationsTheRunProduced()
    {
        using var partition = fixture.ReadPartition("http");

        var entries = partition.RootElement.GetProperty("entries").EnumerateArray().ToArray();

        Assert.NotEmpty(entries);
        Assert.Contains(entries, entry => entry.GetProperty("relation_kind").GetString() == "http-call");
        Assert.Contains(entries, entry => entry.GetProperty("relation_kind").GetString() == "http-client");
        Assert.All(entries, entry => Assert.Equal("http", entry.GetProperty("partition").GetString()));
    }

    [Fact]
    public void EveryRelationPartitionMember_HasItsOwnWrittenAggregateFile()
    {
        var written = fixture.WrittenPartitionNames();

        Assert.Equal(Enum.GetValues<RelationPartition>().Length, written.Length);
        Assert.Contains("structural", written, StringComparer.Ordinal);
        Assert.Contains("data", written, StringComparer.Ordinal);
    }

    [Fact]
    public void DataPartitionFile_CarriesThePersistenceRelationsPassTwoResolved()
    {
        Assert.True(File.Exists(fixture.PartitionPath("data")));

        using var partition = fixture.ReadPartition("data");

        Assert.Equal("data", partition.RootElement.GetProperty("kind").GetString());
        Assert.Equal(2, partition.RootElement.GetProperty("schema_version").GetInt32());
        var entries = partition.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.Equal("data", entry.GetProperty("partition").GetString()));
        Assert.All(entries, entry =>
            Assert.NotEmpty(entry.GetProperty("header").GetProperty("evidence").EnumerateArray()));
        Assert.Contains(entries, entry => entry.GetProperty("relation_kind").GetString() == "exposes");
    }

    [Fact]
    public void DataPartitionFile_RecordsTheFixturesUnconfiguredEntityAsAConventionMapping()
    {
        using var partition = fixture.ReadPartition("data");

        // The fixture's OrderLine has no ToTable anywhere in the run, so it has no proven table: the
        // mapping names the DbSet property, states it is a convention, and targets nothing.
        var mapsTo = partition.RootElement.GetProperty("entries").EnumerateArray()
            .Where(entry => entry.GetProperty("relation_kind").GetString() == "maps-to")
            .Select(entry => (Entry: entry, Details: Details(entry)))
            .ToArray();

        var convention = Assert.Single(mapsTo, mapping => mapping.Details["mapping"] == "convention");
        Assert.Equal("convention-mapping", convention.Entry.GetProperty("unresolved_reason").GetString());
        Assert.False(convention.Entry.TryGetProperty("target_id", out _));
        Assert.Equal("OrderLines", convention.Details["target_text"]);

        // Order is configured in a different document, so its one mapping is the configured one -
        // no second convention mapping is emitted beside it.
        var configured = Assert.Single(mapsTo, mapping => mapping.Details["mapping"] == "configured");
        Assert.Equal("exact", configured.Entry.GetProperty("header").GetProperty("resolution").GetString());
        Assert.Equal("order_headers", configured.Details["target_text"]);
        Assert.NotNull(configured.Entry.GetProperty("target_id").GetString());
        Assert.False(configured.Entry.TryGetProperty("unresolved_reason", out _));
    }

    private static Dictionary<string, string> Details(JsonElement entry) =>
        entry.GetProperty("details").EnumerateArray().ToDictionary(
            detail => detail.GetProperty("key").GetString()!,
            detail => detail.GetProperty("value").GetString()!,
            StringComparer.Ordinal);

    [Fact]
    public void StructuralPartitionFile_CarriesTheFixturesCallsCreatesAndReferencesRelations()
    {
        using var partition = fixture.ReadPartition("structural");

        var kinds = partition.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => entry.GetProperty("relation_kind").GetString())
            .ToArray();

        Assert.Contains("calls", kinds, StringComparer.Ordinal);
        Assert.Contains("creates", kinds, StringComparer.Ordinal);
        Assert.Contains("references", kinds, StringComparer.Ordinal);
    }
}

public sealed class AggregateRelationPartitionFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-agg-partitions-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-agg-partitions-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(manifest, Output, topic: "acme-aggregate-partitions", domain: "system-design").Request);
        var result = await new AnalysisEngine().AnalyzeAsync(request);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
        }
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(Output))
        {
            Directory.Delete(Output, recursive: true);
        }

        return Task.CompletedTask;
    }

    public JsonDocument ReadPartition(string wireName) =>
        JsonDocument.Parse(File.ReadAllText(PartitionPath(wireName)));

    public string PartitionPath(string wireName) =>
        Path.Combine(PartitionDirectory, wireName + ".json");

    public string PartitionDirectory => Path.Combine(TopicLayout.RawRoot(Output), "facts", "relations");

    public string[] WrittenPartitionNames() =>
        Directory.EnumerateFiles(PartitionDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            // resolution.json (RELR-35) is a metrics summary, not a RelationPartition member's own file.
            .Where(static name => name != "resolution")
            .Order(StringComparer.Ordinal)
            .ToArray()!;
}
