using Csharp2Md.Domain.Facets;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Tests.Composition;

public sealed class HttpCorrelatorTests
{
    private static readonly string Outbound = FacetAxes.WireValue(BoundaryDirection.Outbound);
    private static readonly string Inbound = FacetAxes.WireValue(BoundaryDirection.Inbound);
    private static readonly string Http = FacetAxes.WireValue(BoundaryProtocol.Http);

    [Fact]
    [Trait("Requirement", "MSC-22")]
    public void Match_OutboundMethodAndRouteJoiningInboundKey_PublishesOneCandidate()
    {
        var source = Solution(
            "solution-orders",
            HttpOperation("fact-out", Outbound, "facts/architecture.json", 2, "POST", "/v1/charges", "payments-client", "POST /v1/charges"));
        var target = Solution(
            "solution-payments",
            HttpOperation("fact-in", Inbound, "facts/architecture.json", 5, "POST", "/v1/charges", destinationScope: null, "POST /v1/charges"));

        var result = HttpCorrelator.Match([source, target]);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("fact-out", candidate.SourceFactId);
        Assert.Equal("solution-orders", candidate.SourceSolutionIdentity);
        Assert.Equal("fact-in", candidate.TargetFactId);
        Assert.Equal("solution-payments", candidate.TargetSolutionIdentity);
        Assert.Equal("POST /v1/charges", candidate.MatchedKey);
        Assert.Equal("payments-client", candidate.DestinationScope);
    }

    [Fact]
    [Trait("Requirement", "MSC-23")]
    public void Match_HttpPairing_NeverReachesCrossSolutionRelations()
    {
        var source = Solution(
            "solution-orders",
            HttpOperation("fact-out", Outbound, "facts/architecture.json", 0, "POST", "/v1/charges", "payments-client", "POST /v1/charges"));
        var target = Solution(
            "solution-payments",
            HttpOperation("fact-in", Inbound, "facts/architecture.json", 0, "POST", "/v1/charges", destinationScope: null, "POST /v1/charges"));
        SolutionContribution[] contributions = [source, target];

        var http = HttpCorrelator.Match(contributions);
        var messaging = MessagingCorrelator.Match(contributions);

        Assert.Single(http.Candidates);
        Assert.Empty(http.Relations);
        Assert.Empty(messaging.Relations);
        Assert.Empty(messaging.Candidates);
    }

    [Fact]
    [Trait("Requirement", "MSC-22")]
    public void Match_OutboundWithNullHttpMethodOrNullRoute_PublishesNoCandidate()
    {
        var inbound = Solution(
            "solution-payments",
            HttpOperation("fact-in", Inbound, "facts/architecture.json", 0, "POST", "/v1/charges", destinationScope: null, "POST /v1/charges"));
        var nullMethod = Solution(
            "solution-orders",
            HttpOperation("fact-out-method", Outbound, "facts/architecture.json", 0, httpMethod: null, "/v1/charges", "payments-client", "POST /v1/charges"));
        var nullRoute = Solution(
            "solution-billing",
            HttpOperation("fact-out-route", Outbound, "facts/architecture.json", 0, "POST", route: null, "payments-client", "POST /v1/charges"));

        Assert.Empty(HttpCorrelator.Match([nullMethod, inbound]).Candidates);
        Assert.Empty(HttpCorrelator.Match([nullRoute, inbound]).Candidates);
    }

    [Fact]
    [Trait("Requirement", "MSC-22")]
    public void Match_InboundBareTemplateWithNoMethod_PublishesNoCandidate()
    {
        // MSC-22 requires the inbound protocol operation key to equal the outbound
        // method and route joined by a single space. Inbound HTTP keys are
        // method + " " + template only when a method observation exists; otherwise
        // BoundaryPass publishes the bare template, which cannot equal that join.
        var source = Solution(
            "solution-orders",
            HttpOperation("fact-out", Outbound, "facts/architecture.json", 0, "POST", "/v1/charges", "payments-client", "POST /v1/charges"));
        var target = Solution(
            "solution-payments",
            HttpOperation("fact-in", Inbound, "facts/architecture.json", 0, httpMethod: null, "/v1/charges", destinationScope: null, "/v1/charges"));

        Assert.Empty(HttpCorrelator.Match([source, target]).Candidates);
    }

    [Fact]
    [Trait("Requirement", "MSC-22")]
    public void Match_DirectionAndProtocolLiteralsComeFromFacetAxesWireValue()
    {
        var path = Path.Combine(
            ProjectionTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Projection",
            "Composition",
            "HttpCorrelator.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("FacetAxes.WireValue(BoundaryDirection.Outbound)", source, StringComparison.Ordinal);
        Assert.Contains("FacetAxes.WireValue(BoundaryDirection.Inbound)", source, StringComparison.Ordinal);
        Assert.Contains("FacetAxes.WireValue(BoundaryProtocol.Http)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"outbound\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"inbound\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"http\"", source, StringComparison.Ordinal);
    }

    private static SolutionContribution Solution(string identity, params ContributedBoundaryOperation[] operations) =>
        new(identity, identity + ".slnx", "s-" + identity, [.. operations], [], [], [], []);

    private static ContributedBoundaryOperation HttpOperation(
        string factId,
        string direction,
        string artifactKey,
        int ordinal,
        string? httpMethod,
        string? route,
        string? destinationScope,
        string? protocolOperationKey) =>
        new(
            factId,
            "BoundaryOperation",
            direction,
            Http,
            destinationScope,
            httpMethod,
            route,
            protocolOperationKey,
            artifactKey,
            ordinal);
}
