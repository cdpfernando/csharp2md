using System.Text.Json.Nodes;
using Csharp2Md.Analysis;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Wire;

/// <summary>
/// GCPC-039 (partial -- full closure is Phase 6's sharding work; GCPC-044 stays Pending until the scale
/// input from Phase 12 exercises it): proves the intern table round-trips, orders deterministically
/// independent of record order, publishes no wrapper when nothing repeats, and shrinks the
/// certification corpus's <c>contains</c> payload below its T20 figure.
/// </summary>
public sealed class InternTableTests
{
    /// <summary>Recorded by T20 as the post-fix (scoped-evidence) <c>contains.json</c> byte size.</summary>
    private const long T20ContainsPayloadBytes = 390_576;

    [Fact]
    public void Encode_ThenDecode_RoundTripsToTheSameRecords()
    {
        var records = RepeatingRecords();

        var encoded = InternTable.Encode(records);
        var decoded = InternTable.Decode(encoded);

        Assert.True(JsonNode.DeepEquals(records, decoded));
    }

    [Fact]
    public void Encode_RepeatingValues_TableCarriesOnlyValuesThatRepeat()
    {
        var records = RepeatingRecords();

        var encoded = InternTable.Encode(records);
        var table = Assert.IsType<JsonArray>(encoded["table"]);
        var values = table.Select(static node => node!.GetValue<string>()).ToArray();

        Assert.Contains("contains", values);
        Assert.Contains("csharp2md.structural.contains", values);
        Assert.DoesNotContain("only-once-a", values);
        Assert.DoesNotContain("only-once-b", values);
    }

    [Fact]
    public void Encode_SameRecordsInDifferentOrder_TableIsIdentical()
    {
        var forward = RepeatingRecords();
        var reversed = new JsonArray(forward.Select(static node => node!.DeepClone()).Reverse().ToArray());

        var forwardTable = InternTable.Encode(forward)["table"]!.ToJsonString();
        var reversedTable = InternTable.Encode(reversed)["table"]!.ToJsonString();

        Assert.Equal(forwardTable, reversedTable);
    }

    [Fact]
    public void Encode_NoRepeatedStrings_PublishesNoWastefulTable()
    {
        var records = new JsonArray(
            new JsonObject { ["id"] = "alpha", ["kind"] = "solitary-alpha" },
            new JsonObject { ["id"] = "beta", ["kind"] = "solitary-beta" });

        var encoded = InternTable.Encode(records);
        var table = Assert.IsType<JsonArray>(encoded["table"]);

        Assert.Empty(table);
        Assert.True(JsonNode.DeepEquals(records, InternTable.Decode(encoded)));
    }

    [Fact]
    public async Task Encode_CertificationCorpusContainsPayload_IsSmallerThanTheT20Figure()
    {
        var solutionPath = Path.Combine(
            StorageTestPaths.RepoRoot,
            "fixtures",
            "CertificationCorpus",
            "CertificationCorpus.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var containsArtifact = Assert.Single(
            publication.ArtifactsInPublicationOrder,
            artifact => artifact.CanonicalKey == "relations/confirmed/contains.json");
        var inlineBytes = containsArtifact.Payload.AsSpan().Length;

        var records = (JsonArray)JsonNode.Parse(containsArtifact.Payload.AsSpan())!;
        var encoded = InternTable.Encode(records);
        var internedBytes = CanonicalJson.Write((JsonNode)encoded).Length;

        Assert.True(
            internedBytes < inlineBytes,
            $"Expected interning to shrink the payload: interned {internedBytes} bytes vs inline {inlineBytes} bytes.");
        Assert.True(
            internedBytes < T20ContainsPayloadBytes,
            $"Expected a reduction against the T20 figure: interned {internedBytes} bytes vs T20 {T20ContainsPayloadBytes} bytes.");
    }

    private static JsonArray RepeatingRecords() =>
    [
        new JsonObject
        {
            ["kind"] = "contains",
            ["classifier_identity"] = "csharp2md.structural.contains",
            ["source_id"] = "s-1",
            ["target_id"] = "t-1",
            ["extra"] = "only-once-a",
        },
        new JsonObject
        {
            ["kind"] = "contains",
            ["classifier_identity"] = "csharp2md.structural.contains",
            ["source_id"] = "s-2",
            ["target_id"] = "t-2",
            ["extra"] = "only-once-b",
        },
    ];
}
