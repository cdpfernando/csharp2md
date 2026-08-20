using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
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
        Path.Combine(TopicLayout.RawRoot(Output), "facts", "relations", wireName + ".json");
}
