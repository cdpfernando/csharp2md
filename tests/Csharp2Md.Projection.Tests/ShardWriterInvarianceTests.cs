using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests;

public sealed class ShardWriterInvarianceTests
{
    private const string CatalogKey = "catalogs/entry-points.json";

    [Fact]
    [Trait("Requirement", "RP-56")]
    public void Write_Split_UnionOfShardEntriesEqualsUnsplitSet()
    {
        var entries = PaddedEntries(12, 40);
        var unsplit = Assert.Single(ShardWriter.Write(CatalogKey, entries, int.MaxValue));
        var split = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 160);

        Assert.True(split.Length > 1);
        Assert.Equal(ReadFactIds(unsplit), UnionFactIds(split));
        Assert.Equal(ReadCanonicalEntries(unsplit), UnionCanonicalEntries(split));
    }

    [Fact]
    [Trait("Requirement", "RP-56")]
    public void Write_Split_PreservesEveryEntryBodyUnchanged()
    {
        var entries = PaddedEntries(10, 50);
        var originals = entries.ToDictionary(
            static pair => pair.FactId,
            static pair => CanonicalJson.Write(pair.Entry.DeepClone()),
            StringComparer.Ordinal);

        var split = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 150);
        Assert.True(split.Length > 1);

        foreach (var fragment in split)
        {
            foreach (var node in Objects(fragment))
            {
                var factId = (string?)node["fact_id"];
                Assert.False(string.IsNullOrEmpty(factId));
                Assert.True(
                    originals[factId].AsSpan().SequenceEqual(CanonicalJson.Write(node.DeepClone()).AsSpan()),
                    factId);
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-56")]
    public void Write_Split_ChangesOnlyWhichArtifactHostsEachEntry()
    {
        var entries = PaddedEntries(10, 45);
        var unsplit = Assert.Single(ShardWriter.Write(CatalogKey, entries, int.MaxValue));
        var split = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 150);

        Assert.Equal(CatalogKey, unsplit.CanonicalKey);
        Assert.All(split, fragment => Assert.NotEqual(CatalogKey, fragment.CanonicalKey));
        Assert.Equal(ReadCanonicalEntries(unsplit), UnionCanonicalEntries(split));
        Assert.NotEqual(
            new[] { unsplit.CanonicalKey },
            split.Select(static fragment => fragment.CanonicalKey).ToArray());
    }

    [Fact]
    [Trait("Requirement", "RP-55")]
    public void Commit_SplitCatalog_ListsEveryShardInTheManifest()
    {
        var publication = PublishOverCeilingCatalogs();
        var shards = publication.ArtifactsInPublicationOrder
            .Where(static fragment => fragment.CanonicalKey.StartsWith("catalogs/entry-points.", StringComparison.Ordinal))
            .Select(static fragment => fragment.CanonicalKey)
            .ToArray();
        var manifest = ReadManifest(publication);

        Assert.True(shards.Length > 1);
        Assert.DoesNotContain(
            publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey == CatalogProjector.EntryPointsKey);
        Assert.All(
            shards,
            key => Assert.Contains(manifest.Artifacts, entry => entry.Path == key));
    }

    [Fact]
    [Trait("Requirement", "RP-55")]
    public void Commit_SplitPosting_ListsEveryShardInTheManifest()
    {
        var publication = Publish(ContainsSnapshot(), ceilingBytes: 80);
        var postingShards = publication.ArtifactsInPublicationOrder
            .Where(static fragment => fragment.CanonicalKey.StartsWith("postings/", StringComparison.Ordinal))
            .Select(static fragment => fragment.CanonicalKey)
            .ToArray();
        var manifest = ReadManifest(publication);

        Assert.Contains(
            postingShards,
            key => key.StartsWith("postings/outgoing.", StringComparison.Ordinal)
                || key.StartsWith("postings/incoming.", StringComparison.Ordinal));
        Assert.All(
            postingShards,
            key => Assert.Contains(manifest.Artifacts, entry => entry.Path == key));
    }

    [Fact]
    [Trait("Requirement", "RP-55")]
    [Trait("Requirement", "RP-56")]
    public void Write_Rerun_ProducesTheSameShardAssignment()
    {
        var entries = PaddedEntries(12, 40);
        var first = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 160);
        var second = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 160);

        Assert.True(first.Length > 1);
        Assert.Equal(
            first.Select(static fragment => fragment.CanonicalKey),
            second.Select(static fragment => fragment.CanonicalKey));
        for (var index = 0; index < first.Length; index++)
        {
            Assert.True(
                first[index].Payload.AsSpan().SequenceEqual(second[index].Payload.AsSpan()),
                first[index].CanonicalKey);
        }
    }

    private static CommittedPublication PublishOverCeilingCatalogs()
    {
        var facts = Enumerable.Range(0, 16)
            .Select(index => (IFact)CatalogProjectionFactory.CreateEntryPoint("Run" + index, "Api" + index))
            .ToImmutableArray();
        return Publish(new FactualSnapshot(facts, [], [], [], [], []), ceilingBytes: 400);
    }

    private static FactualSnapshot ContainsSnapshot()
    {
        var solution = CatalogProjectionFactory.CreateSolutionFact();
        var project = CatalogProjectionFactory.CreateProjectFact("src/Acme.Orders/Acme.Orders.csproj");
        var document = CatalogProjectionFactory.CreateDocumentFact(
            "src/Acme.Orders/Acme.Orders.csproj",
            "src/Acme.Orders/Program.cs");
        return new FactualSnapshot(
            [solution, project, document],
            [],
            [
                CatalogProjectionFactory.Contains(solution.Reference, project.Reference, 1),
                CatalogProjectionFactory.Contains(project.Reference, document.Reference, 2),
            ],
            [],
            [],
            []);
    }

    private static CommittedPublication Publish(FactualSnapshot snapshot, int ceilingBytes)
    {
        var store = new InMemoryTransactionalStore(new PackageProjector(ceilingBytes));
        var session = store.Open("s-test", new EmptySourceReader());
        session.Stage(snapshot);
        return session.Commit();
    }

    private static ManifestEnvelope ReadManifest(CommittedPublication publication) =>
        CanonicalJson.Read<ManifestEnvelope>(
            publication.ArtifactsInPublicationOrder
                .Single(static fragment => fragment.CanonicalKey == "manifest.json")
                .Payload
                .AsSpan());

    private static IReadOnlyList<(string FactId, JsonNode Entry)> PaddedEntries(int count, int padding) =>
        Enumerable.Range(0, count)
            .Select(index => (
                "id1:entrypoint:" + index,
                (JsonNode)new JsonObject
                {
                    ["fact_id"] = "id1:entrypoint:" + index,
                    ["display_name"] = "Name" + index,
                    ["padding"] = new string('x', padding),
                }))
            .ToArray();

    private static string[] ReadFactIds(StagedFragment fragment) =>
        Objects(fragment)
            .Select(static node => (string?)node["fact_id"] ?? string.Empty)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

    private static string[] UnionFactIds(ImmutableArray<StagedFragment> fragments) =>
        fragments.SelectMany(ReadFactIds).OrderBy(static id => id, StringComparer.Ordinal).ToArray();

    private static string[] ReadCanonicalEntries(StagedFragment fragment) =>
        Objects(fragment)
            .Select(static node => EncodingUtf8(CanonicalJson.Write(node.DeepClone())))
            .OrderBy(static json => json, StringComparer.Ordinal)
            .ToArray();

    private static string[] UnionCanonicalEntries(ImmutableArray<StagedFragment> fragments) =>
        fragments
            .SelectMany(ReadCanonicalEntries)
            .OrderBy(static json => json, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<JsonObject> Objects(StagedFragment fragment)
    {
        foreach (var node in ParseArray(fragment))
        {
            yield return Assert.IsType<JsonObject>(node);
        }
    }

    private static JsonArray ParseArray(StagedFragment fragment)
    {
        var array = JsonNode.Parse(fragment.Payload.AsSpan()) as JsonArray;
        Assert.NotNull(array);
        return array;
    }

    private static string EncodingUtf8(ImmutableArray<byte> utf8) =>
        System.Text.Encoding.UTF8.GetString(utf8.AsSpan());
}
