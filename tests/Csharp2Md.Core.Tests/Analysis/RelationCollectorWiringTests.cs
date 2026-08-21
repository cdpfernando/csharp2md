using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// T12/T25: proves relations are wired into the live <see cref="AnalysisEngine"/> pipeline in the
/// default, syntax-only analysis mode - spec.md's P1 and P2 Independent Tests against the real
/// <c>fixtures/SyntheticSolution</c> fixture. Migrated for RELR-01/AD-018: relations now live in the
/// resolver's one solution-level fragment (<c>raw/facts/relations/*.json</c>), never in a document
/// fragment, so every assertion here reads the partition files instead of a document's own <c>Relations</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RelationCollectorWiringTests(RelationCollectorWiringFixture fixture)
    : IClassFixture<RelationCollectorWiringFixture>
{
    [Fact]
    public void AnalyzeAsync_SyntaxOnlyMode_PaymentsServiceDocument_EmitsSubscribeHandlePublishAndInheritsRelations()
    {
        var relations = fixture.FindRelationsFor("PaymentsService.cs");

        Assert.Contains(relations, relation =>
            RelationKind(relation) == "subscribes"
            && HasDetail(relation, "target_text", "OrderPlaced"));
        Assert.Contains(relations, relation =>
            RelationKind(relation) == "handles"
            && HasDetail(relation, "target_text", "OrderPlaced"));
        Assert.Contains(relations, relation =>
            RelationKind(relation) == "publishes"
            && HasDetail(relation, "target_text", "PaymentProcessed"));
        Assert.Contains(relations, relation =>
            RelationKind(relation) == "inherits"
            && Details(relation).Any(detail =>
                detail.GetProperty("key").GetString() == "target_text"
                && detail.GetProperty("value").GetString() is "PaymentsBase" or "Payments.PaymentsBase"));
    }

    [Fact]
    public void AnalyzeAsync_SyntaxOnlyMode_OrderServiceDocument_EmitsHttpClientHttpCallAndCallsRelations()
    {
        var relations = fixture.FindRelationsFor("OrderService.cs");

        Assert.Contains(relations, relation =>
            RelationKind(relation) == "http-client"
            && HasDetail(relation, "target_text", "PaymentService"));
        Assert.Contains(relations, relation => RelationKind(relation) == "http-call");
        Assert.Contains(relations, relation =>
            RelationKind(relation) == "calls"
            && Details(relation).Any(detail =>
                detail.GetProperty("key").GetString() == "target_text"
                && detail.GetProperty("value").GetString()!.Contains("AuthorizePayment", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Migrated from asserting every relation was unresolved (the pre-resolver placeholder, RELR-16
    /// deleted it) to the invariant the resolver actually guarantees (C2M-FV-007/008): a relation's
    /// <c>target_id</c> and <c>unresolved_reason</c> are never both absent, and never both present.
    /// Strictly more than the original claim proved, since the original could not distinguish "correctly
    /// unresolved" from "the pipeline never tried" - the resolver now genuinely attempts resolution, and
    /// this proves at least one PaymentsService relation resolves to a real target as evidence it did.
    /// </summary>
    [Fact]
    public void AnalyzeAsync_SyntaxOnlyMode_EveryEmittedRelation_HasASelfConsistentResolutionOutcome()
    {
        var relations = fixture.FindRelationsFor("PaymentsService.cs");

        Assert.NotEmpty(relations);
        Assert.All(relations, relation =>
        {
            var hasTarget = relation.TryGetProperty("target_id", out var target) && target.ValueKind != JsonValueKind.Null;
            var hasReason = relation.TryGetProperty("unresolved_reason", out var reason)
                && reason.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(reason.GetString());
            Assert.True(hasTarget != hasReason, $"Relation '{relation.GetProperty("relation_id").GetString()}' must carry exactly one of target_id or unresolved_reason.");
        });
        Assert.Contains(relations, relation =>
            relation.TryGetProperty("target_id", out var target) && target.ValueKind != JsonValueKind.Null);
    }

    private static string RelationKind(JsonElement relation) => relation.GetProperty("relation_kind").GetString()!;

    private static IEnumerable<JsonElement> Details(JsonElement relation) =>
        relation.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array
            ? details.EnumerateArray()
            : [];

    private static bool HasDetail(JsonElement relation, string key, string value) =>
        Details(relation).Any(detail => detail.GetProperty("key").GetString() == key && detail.GetProperty("value").GetString() == value);
}

public sealed class RelationCollectorWiringFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-relc-wiring-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-relc-wiring-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(manifest, Output, topic: "acme-relation-wiring", domain: "system-design").Request);
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

    /// <summary>
    /// Every relation, across every partition file, whose evidence names <paramref name="fileName"/> -
    /// the resolver's one solution-level fragment (AD-018) replaces the per-document lookup this fixture
    /// used to do against <c>raw/facts/document/*.json</c>.
    /// </summary>
    public JsonElement[] FindRelationsFor(string fileName)
    {
        var relationsRoot = Path.Combine(TopicLayout.RawRoot(Output), "facts", "relations");
        var matches = new List<JsonElement>();
        foreach (var path in Directory.EnumerateFiles(relationsRoot, "*.json"))
        {
            using var partition = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var entry in partition.RootElement.GetProperty("entries").EnumerateArray())
            {
                var evidence = entry.GetProperty("header").GetProperty("evidence");
                if (evidence.EnumerateArray().Any(item =>
                    item.GetProperty("relative_path").GetString()!.EndsWith(fileName, StringComparison.Ordinal)))
                {
                    matches.Add(entry.Clone());
                }
            }
        }

        return [.. matches];
    }
}
