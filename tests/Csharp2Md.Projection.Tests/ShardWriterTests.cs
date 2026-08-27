using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Projection.Tests.Postings;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests;

public sealed class ShardWriterTests
{
    private const string CatalogKey = "catalogs/entry-points.json";

    [Fact]
    [Trait("Requirement", "RP-52")]
    public void DefaultCeiling_IsOneMebibyteAndWriteAcceptsAnOverride()
    {
        Assert.Equal(1024 * 1024, ShardWriter.DefaultCeilingBytes);

        var entries = PaddedEntries(count: 4, padding: 80);
        var unsplit = ShardWriter.Write(CatalogKey, entries, ShardWriter.DefaultCeilingBytes);
        var split = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 200);

        Assert.Equal(CatalogKey, Assert.Single(unsplit).CanonicalKey);
        Assert.True(split.Length > 1, "A lowered ceiling must split the same catalog.");
        Assert.All(split, fragment => Assert.NotEqual(CatalogKey, fragment.CanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "RP-53")]
    public void Write_SyntheticOverCeilingInput_SplitsIntoBucketAddressedShards()
    {
        var entries = PaddedEntries(count: 8, padding: 60);
        var fragments = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 180);

        Assert.True(fragments.Length > 1);
        Assert.All(
            fragments,
            fragment =>
            {
                Assert.StartsWith("catalogs/entry-points.", fragment.CanonicalKey, StringComparison.Ordinal);
                Assert.Matches(@"^catalogs/entry-points\.[0-9a-f]{2}\.json$", fragment.CanonicalKey);
            });
        Assert.Equal(
            fragments.Select(static fragment => fragment.CanonicalKey).OrderBy(static key => key, StringComparer.Ordinal),
            fragments.Select(static fragment => fragment.CanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "RP-52")]
    [Trait("Requirement", "RP-53")]
    public void Project_FixtureScaleCatalogs_DoNotSplitAtTheDefaultCeiling()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"),
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"));

        var fragments = CatalogProjector.Project(view);

        Assert.NotEmpty(fragments);
        Assert.All(
            fragments,
            fragment => Assert.DoesNotMatch(@"\.[0-9a-f]{2}\.json$", fragment.CanonicalKey));
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == CatalogProjector.EntryPointsKey);
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == CatalogProjector.BoundaryOperationsKey);
        Assert.All(fragments, fragment => Assert.True(fragment.Payload.Length < ShardWriter.DefaultCeilingBytes));
    }

    [Fact]
    [Trait("Requirement", "RP-53")]
    public void Project_OverCeilingCatalog_SplitsThroughCatalogProjector()
    {
        var facts = Enumerable.Range(0, 16)
            .Select(index => CatalogProjectionFactory.CreateEntryPoint("Run" + index, "Api" + index))
            .ToArray();
        var view = CatalogProjectionFactory.ViewOf(facts);

        var split = CatalogProjector.Project(view, ceilingBytes: 400);

        Assert.True(
            split.Count(fragment => fragment.CanonicalKey.StartsWith("catalogs/entry-points.", StringComparison.Ordinal)) > 1);
        Assert.DoesNotContain(split, fragment => fragment.CanonicalKey == CatalogProjector.EntryPointsKey);
    }

    [Fact]
    [Trait("Requirement", "RP-53")]
    public void Project_OverCeilingPosting_SplitsThroughPostingProjector()
    {
        var view = PostingProjectionFactory.ContainsView();
        var split = PostingProjector.Project(view, ceilingBytes: 120);

        Assert.True(
            split.Count(fragment => fragment.CanonicalKey.StartsWith("postings/outgoing.", StringComparison.Ordinal))
            + split.Count(fragment => fragment.CanonicalKey.StartsWith("postings/incoming.", StringComparison.Ordinal))
            >= 1);
        Assert.True(split.Length > 2);
    }

    [Fact]
    [Trait("Requirement", "RP-54")]
    public void Write_BucketKeys_DeriveFromFactIdSha256Prefix()
    {
        var factId = "id1:entrypoint:alpha";
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(factId)))[..2];
        var entries = new List<(string FactId, JsonNode Entry)>
        {
            Entry(factId, "Alpha", padding: 40),
            Entry("id1:entrypoint:zeta", "Zeta", padding: 40),
            Entry("id1:entrypoint:mid", "Mid", padding: 40),
        };

        var fragments = ShardWriter.Write(CatalogKey, entries, ceilingBytes: 80);
        var assigned = Assert.Single(
            fragments,
            fragment => fragment.CanonicalKey.Contains("." + expected + ".", StringComparison.Ordinal));

        Assert.Equal("catalogs/entry-points." + expected + ".json", assigned.CanonicalKey);
    }

    [Fact]
    [Trait("Requirement", "RP-54")]
    public void Write_RenamingEveryDisplayName_YieldsTheSameBucketAssignment()
    {
        var factIds = Enumerable.Range(0, 10).Select(index => "id1:entrypoint:" + index).ToArray();
        var original = factIds.Select(id => Entry(id, "Alpha-" + id, padding: 50)).ToArray();
        var renamed = factIds.Select(id => Entry(id, "Zeta-" + id, padding: 50)).ToArray();

        var originalKeys = ShardWriter.Write(CatalogKey, original, ceilingBytes: 160)
            .Select(static fragment => fragment.CanonicalKey)
            .ToArray();
        var renamedKeys = ShardWriter.Write(CatalogKey, renamed, ceilingBytes: 160)
            .Select(static fragment => fragment.CanonicalKey)
            .ToArray();

        Assert.True(originalKeys.Length > 1);
        Assert.Equal(originalKeys, renamedKeys);
        Assert.Equal(
            Assignment(original, 160),
            Assignment(renamed, 160));
    }

    private static IReadOnlyList<(string FactId, JsonNode Entry)> PaddedEntries(int count, int padding) =>
        Enumerable.Range(0, count)
            .Select(index => Entry("id1:entrypoint:" + index, "Name" + index, padding))
            .ToArray();

    private static (string FactId, JsonNode Entry) Entry(string factId, string displayName, int padding) =>
        (factId, new JsonObject
        {
            ["fact_id"] = factId,
            ["display_name"] = displayName,
            ["padding"] = new string('x', padding),
        });

    private static IReadOnlyDictionary<string, string> Assignment(
        IReadOnlyList<(string FactId, JsonNode Entry)> entries,
        int ceilingBytes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fragment in ShardWriter.Write(CatalogKey, entries, ceilingBytes))
        {
            var array = JsonNode.Parse(fragment.Payload.AsSpan()) as JsonArray;
            Assert.NotNull(array);
            foreach (var node in array)
            {
                var factId = (string?)node?["fact_id"];
                Assert.False(string.IsNullOrEmpty(factId));
                map[factId] = fragment.CanonicalKey;
            }
        }

        return map;
    }
}
