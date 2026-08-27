using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Tests.Composition;

public sealed class BatchComposerComposeTests
{
    private static readonly string Outbound = FacetAxes.WireValue(BoundaryDirection.Outbound);
    private static readonly string Inbound = FacetAxes.WireValue(BoundaryDirection.Inbound);
    private static readonly string Messaging = FacetAxes.WireValue(BoundaryProtocol.Messaging);
    private static readonly string Http = FacetAxes.WireValue(BoundaryProtocol.Http);

    [Fact]
    [Trait("Requirement", "MSC-26")]
    public void Compose_OrdersRelationsAndCandidatesBySourceThenTargetIdentities()
    {
        var zeta = Solution(
            "solution-zeta",
            operations:
            [
                MessagingOperation("fact-z-out", Outbound, "orders.v1.Late"),
                MessagingOperation("fact-z-in", Inbound, "orders.v1.Early"),
                HttpOperation("fact-z-http-out", Outbound, "POST", "/late", "zeta-client", "POST /late"),
                HttpOperation("fact-z-http-in", Inbound, "GET", "/early", destinationScope: null, "GET /early"),
            ]);
        var alpha = Solution(
            "solution-alpha",
            operations:
            [
                MessagingOperation("fact-a-out", Outbound, "orders.v1.Early"),
                MessagingOperation("fact-a-in", Inbound, "orders.v1.Late"),
                HttpOperation("fact-a-http-out", Outbound, "GET", "/early", "alpha-client", "GET /early"),
                HttpOperation("fact-a-http-in", Inbound, "POST", "/late", destinationScope: null, "POST /late"),
            ]);

        var fragments = new BatchComposer().Compose(View(zeta, alpha));
        var relations = ReadArray(fragments, BatchComposer.CrossSolutionRelationsKey);
        var candidates = ReadArray(fragments, BatchComposer.CorrelationCandidatesKey);

        AssertOrder(relations);
        AssertOrder(candidates);
    }

    [Fact]
    [Trait("Requirement", "MSC-28")]
    public void Compose_ArtifactExceedingCeiling_SplitsIntoOrdinalSuffixedShards()
    {
        var pairs = Enumerable.Range(0, 8)
            .Select(index => (
                Source: Solution(
                    "solution-source-" + index.ToString("D2"),
                    operations: [MessagingOperation("fact-out-" + index, Outbound, "topic-" + index + new string('x', 40))]),
                Target: Solution(
                    "solution-target-" + index.ToString("D2"),
                    operations: [MessagingOperation("fact-in-" + index, Inbound, "topic-" + index + new string('x', 40))])))
            .ToArray();
        var contributions = pairs.SelectMany(static pair => new[] { pair.Source, pair.Target }).ToArray();

        var unsplit = new BatchComposer().Compose(View(contributions));
        var split = new BatchComposer(ceilingBytes: 200).Compose(View(contributions));

        Assert.Contains(unsplit, fragment => fragment.CanonicalKey == BatchComposer.CrossSolutionRelationsKey);
        Assert.DoesNotContain(split, fragment => fragment.CanonicalKey == BatchComposer.CrossSolutionRelationsKey);
        var shards = split
            .Where(static fragment => fragment.CanonicalKey.StartsWith("composition/cross-solution-relations.", StringComparison.Ordinal))
            .ToArray();
        Assert.True(shards.Length > 1, "A lowered ceiling must split the relations artifact.");
        Assert.All(
            shards,
            fragment => Assert.Matches(@"^composition/cross-solution-relations\.[0-9a-f]{2}\.json$", fragment.CanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "MSC-29")]
    public void Compose_ZeroEntryArtifact_IsNotEmitted()
    {
        var orders = Solution(
            "solution-orders",
            components: [Named("fact-comp-orders", "ordering-api")]);
        var catalog = Solution(
            "solution-catalog",
            components: [Named("fact-comp-catalog", "catalog-api")]);

        var fragments = new BatchComposer().Compose(View(orders, catalog));

        Assert.Contains(fragments, fragment => fragment.CanonicalKey == BatchComposer.ComponentsAndDeploymentUnitsKey);
        Assert.DoesNotContain(
            fragments,
            fragment => fragment.CanonicalKey.StartsWith("composition/cross-solution-relations", StringComparison.Ordinal));
        Assert.DoesNotContain(
            fragments,
            fragment => fragment.CanonicalKey.StartsWith("composition/shared-contracts", StringComparison.Ordinal));
        Assert.DoesNotContain(
            fragments,
            fragment => fragment.CanonicalKey.StartsWith("composition/correlation-candidates", StringComparison.Ordinal));
        Assert.DoesNotContain(
            fragments,
            fragment => fragment.CanonicalKey.StartsWith("composition/external-systems", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "MSC-36")]
    public void Compose_ContributionsWithNoCatalogFacts_EmitsNoCompositionFragment()
    {
        var empty = new SolutionContribution("solution-empty", "empty.slnx", "s-empty", [], [], [], [], []);

        Assert.Empty(new BatchComposer().Compose(View(empty)));
        Assert.Empty(new BatchComposer().Compose(new BatchView([], [])));
    }

    [Fact]
    [Trait("Requirement", "MSC-26")]
    public void Compose_ReorderedContributions_ProduceByteIdenticalFragments()
    {
        var orders = Solution(
            "solution-orders",
            operations:
            [
                MessagingOperation("fact-out", Outbound, "orders.v1.OrderPlaced"),
                HttpOperation("fact-http-out", Outbound, "POST", "/v1/charges", "payments-client", "POST /v1/charges"),
            ],
            contracts: [Contract("contract:orders.v1.OrderPlaced", 1)],
            components: [Named("fact-comp-orders", "ordering-api")]);
        var catalog = Solution(
            "solution-catalog",
            operations: [MessagingOperation("fact-in", Inbound, "orders.v1.OrderPlaced")],
            contracts: [Contract("contract:orders.v1.OrderPlaced", 2)]);
        var payments = Solution(
            "solution-payments",
            operations: [HttpOperation("fact-http-in", Inbound, "POST", "/v1/charges", destinationScope: null, "POST /v1/charges")],
            externalSystems: [Named("fact-ext", "stripe-gateway")]);

        var first = new BatchComposer().Compose(View(orders, catalog, payments));
        var second = new BatchComposer().Compose(View(payments, orders, catalog));

        Assert.Equal(
            first.Select(static fragment => fragment.CanonicalKey),
            second.Select(static fragment => fragment.CanonicalKey));
        Assert.Equal(
            first.Select(static fragment => Convert.ToHexString(fragment.Payload.AsSpan())),
            second.Select(static fragment => Convert.ToHexString(fragment.Payload.AsSpan())));
    }

    private static void AssertOrder(JsonArray array)
    {
        var keys = array.Select(static node => (
                node!["source_solution_identity"]!.GetValue<string>(),
                node["source_fact_id"]!.GetValue<string>(),
                node["target_solution_identity"]!.GetValue<string>(),
                node["target_fact_id"]!.GetValue<string>()))
            .ToArray();
        var ordered = keys
            .OrderBy(static key => key.Item1, StringComparer.Ordinal)
            .ThenBy(static key => key.Item2, StringComparer.Ordinal)
            .ThenBy(static key => key.Item3, StringComparer.Ordinal)
            .ThenBy(static key => key.Item4, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ordered, keys);
        Assert.True(keys.Length >= 2);
        Assert.True(
            StringComparer.Ordinal.Compare(keys[0].Item1 + keys[0].Item2, keys[^1].Item1 + keys[^1].Item2) < 0
            || StringComparer.Ordinal.Compare(keys[0].Item3 + keys[0].Item4, keys[^1].Item3 + keys[^1].Item4) < 0);
    }

    private static JsonArray ReadArray(ImmutableArray<StagedFragment> fragments, string canonicalKey)
    {
        var fragment = Assert.Single(fragments, item => item.CanonicalKey == canonicalKey);
        var array = JsonNode.Parse(fragment.Payload.AsSpan()) as JsonArray;
        Assert.NotNull(array);
        return array;
    }

    private static BatchView View(params SolutionContribution[] contributions) =>
        new([], [.. contributions]);

    private static SolutionContribution Solution(
        string identity,
        ContributedBoundaryOperation[]? operations = null,
        ContributedIdentity[]? contracts = null,
        ContributedNamedIdentity[]? components = null,
        ContributedNamedIdentity[]? deploymentUnits = null,
        ContributedNamedIdentity[]? externalSystems = null) =>
        new(
            identity,
            identity + ".slnx",
            "s-" + identity,
            [.. operations ?? []],
            [.. contracts ?? []],
            [.. components ?? []],
            [.. deploymentUnits ?? []],
            [.. externalSystems ?? []]);

    private static ContributedBoundaryOperation MessagingOperation(string factId, string direction, string key) =>
        new(factId, "BoundaryOperation", direction, Messaging, null, null, null, key, "facts/architecture.json", 0);

    private static ContributedBoundaryOperation HttpOperation(
        string factId,
        string direction,
        string? httpMethod,
        string? route,
        string? destinationScope,
        string? protocolOperationKey) =>
        new(factId, "BoundaryOperation", direction, Http, destinationScope, httpMethod, route, protocolOperationKey, "facts/architecture.json", 0);

    private static ContributedIdentity Contract(string factId, int ordinal) =>
        new(factId, "Contract", "facts/contracts.json", ordinal);

    private static ContributedNamedIdentity Named(string factId, string name) =>
        new(factId, "Component", name, "facts/architecture.json", 0);
}
