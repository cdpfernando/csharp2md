using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// T16/T25: the single-place, literal restatement of spec.md's P1 and P2 Independent Tests, run against
/// the real <c>fixtures/SyntheticSolution</c> fixture in the default (syntax-only) mode - the exact
/// scenario the originally-reported defect (<c>relations: []</c>) described. Migrated for RELR-01/AD-018:
/// relations now live in the resolver's one solution-level fragment (<c>raw/facts/relations/*.json</c>),
/// never in a document fragment.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RelationCollectorEndToEndTests(RelationCollectorEndToEndFixture fixture)
    : IClassFixture<RelationCollectorEndToEndFixture>
{
    [Fact]
    public void P1_PaymentsServiceDocument_IncludesSubscribeHandlePublishRelations_WithTargetTextSet()
    {
        var payments = fixture.FindRelationsFor("PaymentsService.cs");

        var subscribes = Assert.Single(payments, relation => RelationKind(relation) == "subscribes");
        AssertTargetText(subscribes, "OrderPlaced");

        var handles = Assert.Single(payments, relation => RelationKind(relation) == "handles");
        AssertTargetText(handles, "OrderPlaced");

        var publishes = Assert.Single(payments, relation => RelationKind(relation) == "publishes");
        AssertTargetText(publishes, "PaymentProcessed");
    }

    [Fact]
    public void P1_OrderServiceDocument_IncludesHttpClientAndHttpCallRelations_WithTargetTextSetAndTargetIdNull()
    {
        var orders = fixture.FindRelationsFor("OrderService.cs");

        // Protocol-target resolution (HTTP) is spec.md's P2, out of scope for this P1-only tasks.md, so
        // both kinds stay genuinely unresolved rather than merely "not yet checked".
        var httpClient = Assert.Single(orders, relation =>
            RelationKind(relation) == "http-client" && HasDetail(relation, "target_text", "PaymentService"));
        AssertUnresolved(httpClient);

        var httpCall = Assert.Single(orders, relation =>
            RelationKind(relation) == "http-call" && HasDetail(relation, "route", "payments/authorize"));
        Assert.True(HasDetail(httpCall, "http_method", "POST"));
        AssertUnresolved(httpCall);
    }

    [Fact]
    public void P1_RelationsAreNoLongerEmptyForTheseDocuments_ClosingTheReportedRelationsEmptyArrayDefect()
    {
        Assert.NotEmpty(fixture.FindRelationsFor("PaymentsService.cs"));
        Assert.NotEmpty(fixture.FindRelationsFor("OrderService.cs"));
    }

    [Fact]
    public void P2_OrderServiceDocument_ProducesAtLeastOneCallsRelationForThePaymentsClientAuthorizePaymentInvocation()
    {
        var orders = fixture.FindRelationsFor("OrderService.cs");

        Assert.Contains(orders, relation =>
            RelationKind(relation) == "calls" && HasDetail(relation, "target_text", "paymentsClient.AuthorizePayment"));
    }

    [Fact]
    public void P2_PaymentsServiceDocument_ProducesInheritsRelationWithTargetTextPaymentsBase()
    {
        var payments = fixture.FindRelationsFor("PaymentsService.cs");

        var inherits = Assert.Single(payments, relation => RelationKind(relation) == "inherits");
        Assert.Contains(Details(inherits), detail =>
            detail.GetProperty("key").GetString() == "target_text"
            && detail.GetProperty("value").GetString() is "PaymentsBase" or "Payments.PaymentsBase");
    }

    private static string RelationKind(JsonElement relation) => relation.GetProperty("relation_kind").GetString()!;

    private static IEnumerable<JsonElement> Details(JsonElement relation) =>
        relation.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array
            ? details.EnumerateArray()
            : [];

    private static bool HasDetail(JsonElement relation, string key, string value) =>
        Details(relation).Any(detail => detail.GetProperty("key").GetString() == key && detail.GetProperty("value").GetString() == value);

    private static void AssertTargetText(JsonElement relation, string expected) =>
        Assert.True(HasDetail(relation, "target_text", expected), $"Expected a target_text detail of '{expected}'.");

    private static void AssertUnresolved(JsonElement relation)
    {
        Assert.False(relation.TryGetProperty("target_id", out var target) && target.ValueKind != JsonValueKind.Null);
        Assert.False(string.IsNullOrWhiteSpace(relation.GetProperty("unresolved_reason").GetString()));
    }
}

public sealed class RelationCollectorEndToEndFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-relc-e2e-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-relc-e2e-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(manifest, Output, topic: "acme-relation-e2e", domain: "system-design").Request);
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
