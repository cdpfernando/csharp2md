using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Tests.Composition;

public sealed class ContractCorrelatorTests
{
    [Fact]
    [Trait("Requirement", "MSC-21")]
    [Trait("Requirement", "MSC-37")]
    public void Match_ContractIdentityInTwoSolutions_PublishesOneEntryNotTwo()
    {
        var orders = Solution(
            "solution-orders",
            Contract("contract:orders.v1.OrderPlaced", "facts/contracts.json", 4));
        var catalog = Solution(
            "solution-catalog",
            Contract("contract:orders.v1.OrderPlaced", "facts/contracts.json", 1));

        var entries = ContractCorrelator.Match([orders, catalog]);

        var entry = Assert.Single(entries);
        Assert.Equal("contract:orders.v1.OrderPlaced", entry.ContractFactId);
        Assert.Equal(2, entry.Owners.Length);
    }

    [Fact]
    [Trait("Requirement", "MSC-21")]
    public void Match_SharedEntry_CarriesSolutionIdentityArtifactKeyAndOrdinalPerOwner()
    {
        var orders = Solution(
            "solution-orders",
            Contract("contract:orders.v1.OrderPlaced", "facts/contracts.json", 4));
        var catalog = Solution(
            "solution-catalog",
            Contract("contract:orders.v1.OrderPlaced", "facts/contracts.json", 1));

        var entry = Assert.Single(ContractCorrelator.Match([orders, catalog]));

        Assert.Contains(
            entry.Owners,
            owner => owner.SolutionIdentity == "solution-orders"
                && owner.ArtifactKey == "facts/contracts.json"
                && owner.Ordinal == 4);
        Assert.Contains(
            entry.Owners,
            owner => owner.SolutionIdentity == "solution-catalog"
                && owner.ArtifactKey == "facts/contracts.json"
                && owner.Ordinal == 1);
    }

    [Fact]
    [Trait("Requirement", "MSC-27")]
    public void Match_OwningSolutions_AreOrderedBySolutionIdentityOrdinalAscending()
    {
        var zeta = Solution(
            "solution-zeta",
            Contract("contract:shared", "facts/contracts.json", 9));
        var alpha = Solution(
            "solution-alpha",
            Contract("contract:shared", "facts/contracts.json", 2));

        var entry = Assert.Single(ContractCorrelator.Match([zeta, alpha]));

        Assert.Equal(["solution-alpha", "solution-zeta"], entry.Owners.Select(static owner => owner.SolutionIdentity));
        Assert.True(
            StringComparer.Ordinal.Compare(entry.Owners[0].SolutionIdentity, entry.Owners[1].SolutionIdentity) < 0);
    }

    [Fact]
    [Trait("Requirement", "MSC-21")]
    public void Match_ContractIdentityInOnlyOneSolution_PublishesNoEntry()
    {
        var orders = Solution(
            "solution-orders",
            Contract("contract:orders.v1.OrderPlaced", "facts/contracts.json", 0));
        var catalog = Solution(
            "solution-catalog",
            Contract("contract:catalog.v1.ItemPriced", "facts/contracts.json", 0));

        Assert.Empty(ContractCorrelator.Match([orders, catalog]));
        Assert.Empty(ContractCorrelator.Match([orders]));
    }

    private static SolutionContribution Solution(string identity, params ContributedIdentity[] contracts) =>
        new(identity, identity + ".slnx", "s-" + identity, [], [.. contracts], [], [], []);

    private static ContributedIdentity Contract(string factId, string artifactKey, int ordinal) =>
        new(factId, "Contract", artifactKey, ordinal);
}
