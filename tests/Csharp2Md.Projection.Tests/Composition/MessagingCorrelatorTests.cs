using Csharp2Md.Domain.Facets;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Tests.Composition;

public sealed class MessagingCorrelatorTests
{
    private static readonly string Outbound = FacetAxes.WireValue(BoundaryDirection.Outbound);
    private static readonly string Inbound = FacetAxes.WireValue(BoundaryDirection.Inbound);
    private static readonly string Messaging = FacetAxes.WireValue(BoundaryProtocol.Messaging);

    [Fact]
    [Trait("Requirement", "MSC-16")]
    [Trait("Requirement", "MSC-17")]
    public void Match_OutboundAndInboundMessagingWithEqualKeyInDifferentSolutions_PublishesOneTargetsEntry()
    {
        var source = Solution(
            "solution-orders",
            MessagingOperation("fact-out", Outbound, "orders.v1.OrderPlaced", "facts/architecture.json", 3));
        var target = Solution(
            "solution-catalog",
            MessagingOperation("fact-in", Inbound, "orders.v1.OrderPlaced", "facts/architecture.json", 7));

        var result = MessagingCorrelator.Match([source, target]);

        var entry = Assert.Single(result.Relations);
        Assert.Empty(result.Candidates);
        Assert.Equal("targets", entry.Kind);
        Assert.Equal("fact-out", entry.SourceFactId);
        Assert.Equal("solution-orders", entry.SourceSolutionIdentity);
        Assert.Equal("facts/architecture.json", entry.SourceArtifactKey);
        Assert.Equal(3, entry.SourceOrdinal);
        Assert.Equal("fact-in", entry.TargetFactId);
        Assert.Equal("solution-catalog", entry.TargetSolutionIdentity);
        Assert.Equal("facts/architecture.json", entry.TargetArtifactKey);
        Assert.Equal(7, entry.TargetOrdinal);
        Assert.Equal("orders.v1.OrderPlaced", entry.MatchedKey);
    }

    [Fact]
    [Trait("Requirement", "MSC-18")]
    public void Match_OneOutboundMatchingInboundOperationsInTwoOtherSolutions_PublishesOneEntryPerPair()
    {
        var source = Solution(
            "solution-orders",
            MessagingOperation("fact-out", Outbound, "orders.v1.OrderPlaced", "facts/architecture.json", 1));
        var catalog = Solution(
            "solution-catalog",
            MessagingOperation("fact-catalog", Inbound, "orders.v1.OrderPlaced", "facts/architecture.json", 2));
        var billing = Solution(
            "solution-billing",
            MessagingOperation("fact-billing", Inbound, "orders.v1.OrderPlaced", "facts/architecture.json", 3));

        var result = MessagingCorrelator.Match([source, catalog, billing]);

        Assert.Equal(2, result.Relations.Length);
        Assert.Empty(result.Candidates);
        Assert.All(result.Relations, entry =>
        {
            Assert.Equal("targets", entry.Kind);
            Assert.Equal("fact-out", entry.SourceFactId);
            Assert.Equal("solution-orders", entry.SourceSolutionIdentity);
            Assert.Equal("orders.v1.OrderPlaced", entry.MatchedKey);
        });
        Assert.Contains(result.Relations, entry => entry.TargetFactId == "fact-catalog" && entry.TargetSolutionIdentity == "solution-catalog");
        Assert.Contains(result.Relations, entry => entry.TargetFactId == "fact-billing" && entry.TargetSolutionIdentity == "solution-billing");
    }

    [Fact]
    [Trait("Requirement", "MSC-19")]
    [Trait("Requirement", "MSC-38")]
    public void Match_OutboundAndInboundInTheSameSolution_PublishesNoEntry()
    {
        var solution = Solution(
            "solution-orders",
            MessagingOperation("fact-out", Outbound, "orders.v1.OrderPlaced", "facts/architecture.json", 1),
            MessagingOperation("fact-in", Inbound, "orders.v1.OrderPlaced", "facts/architecture.json", 2));

        var result = MessagingCorrelator.Match([solution]);

        Assert.Empty(result.Relations);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    [Trait("Requirement", "MSC-20")]
    public void Match_OutboundMatchingNoInboundInAnyOtherSolution_PublishesNeitherRelationNorCandidate()
    {
        var source = Solution(
            "solution-orders",
            MessagingOperation("fact-out", Outbound, "orders.v1.Unmatched", "facts/architecture.json", 1));
        var other = Solution(
            "solution-catalog",
            MessagingOperation("fact-in", Inbound, "orders.v1.Different", "facts/architecture.json", 2));

        var result = MessagingCorrelator.Match([source, other]);

        Assert.Empty(result.Relations);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    [Trait("Requirement", "MSC-16")]
    public void Match_DirectionAndProtocolLiteralsComeFromFacetAxesWireValue()
    {
        var path = Path.Combine(
            ProjectionTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Projection",
            "Composition",
            "MessagingCorrelator.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("FacetAxes.WireValue(BoundaryDirection.Outbound)", source, StringComparison.Ordinal);
        Assert.Contains("FacetAxes.WireValue(BoundaryDirection.Inbound)", source, StringComparison.Ordinal);
        Assert.Contains("FacetAxes.WireValue(BoundaryProtocol.Messaging)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"outbound\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"inbound\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"messaging\"", source, StringComparison.Ordinal);
    }

    private static SolutionContribution Solution(string identity, params ContributedBoundaryOperation[] operations) =>
        new(identity, identity + ".slnx", "s-" + identity, [.. operations], [], [], [], []);

    private static ContributedBoundaryOperation MessagingOperation(
        string factId,
        string direction,
        string protocolOperationKey,
        string artifactKey,
        int ordinal) =>
        new(
            factId,
            "BoundaryOperation",
            direction,
            Messaging,
            DestinationScope: null,
            HttpMethod: null,
            Route: null,
            protocolOperationKey,
            artifactKey,
            ordinal);
}
