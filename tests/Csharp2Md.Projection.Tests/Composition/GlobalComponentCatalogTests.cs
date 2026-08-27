using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Tests.Composition;

public sealed class GlobalComponentCatalogTests
{
    [Fact]
    [Trait("Requirement", "MSC-30")]
    public void Group_EmitsEveryComponentAndDeploymentUnitWithIdentitySolutionAndLocator()
    {
        var orders = Solution(
            "solution-orders",
            components: [Named("fact-comp-orders", "ordering-api", "facts/architecture.json", 1)],
            deploymentUnits: [Named("fact-du-orders", "ordering-container", "facts/architecture.json", 2)]);
        var catalog = Solution(
            "solution-catalog",
            components: [Named("fact-comp-catalog", "catalog-api", "facts/architecture.json", 3)],
            deploymentUnits: [Named("fact-du-catalog", "catalog-container", "facts/architecture.json", 4)]);

        var groups = GlobalComponentCatalog.Group([orders, catalog]);
        var entries = groups.SelectMany(static group => group.Entries).ToArray();

        Assert.Equal(4, entries.Length);
        Assert.Contains(entries, entry =>
            entry.FactId == "fact-comp-orders"
            && entry.SolutionIdentity == "solution-orders"
            && entry.ArtifactKey == "facts/architecture.json"
            && entry.Ordinal == 1);
        Assert.Contains(entries, entry =>
            entry.FactId == "fact-du-orders"
            && entry.SolutionIdentity == "solution-orders"
            && entry.ArtifactKey == "facts/architecture.json"
            && entry.Ordinal == 2);
        Assert.Contains(entries, entry =>
            entry.FactId == "fact-comp-catalog"
            && entry.SolutionIdentity == "solution-catalog"
            && entry.ArtifactKey == "facts/architecture.json"
            && entry.Ordinal == 3);
        Assert.Contains(entries, entry =>
            entry.FactId == "fact-du-catalog"
            && entry.SolutionIdentity == "solution-catalog"
            && entry.ArtifactKey == "facts/architecture.json"
            && entry.Ordinal == 4);
    }

    [Fact]
    [Trait("Requirement", "MSC-31")]
    public void Group_OrdersGroupsByCanonicalNameAndEntriesBySolutionIdentity()
    {
        var zeta = Solution(
            "solution-zeta",
            components: [Named("fact-zeta-alpha", "alpha-api", "facts/architecture.json", 8)],
            deploymentUnits: [Named("fact-zeta-zeta", "zeta-api", "facts/architecture.json", 9)]);
        var alpha = Solution(
            "solution-alpha",
            components: [Named("fact-alpha-alpha", "alpha-api", "facts/architecture.json", 1)],
            deploymentUnits: [Named("fact-alpha-zeta", "zeta-api", "facts/architecture.json", 2)]);

        var groups = GlobalComponentCatalog.Group([zeta, alpha]);

        Assert.Equal(["alpha-api", "zeta-api"], groups.Select(static group => group.CanonicalName));
        Assert.True(StringComparer.Ordinal.Compare(groups[0].CanonicalName, groups[1].CanonicalName) < 0);
        Assert.Equal(
            ["solution-alpha", "solution-zeta"],
            groups[0].Entries.Select(static entry => entry.SolutionIdentity));
        Assert.Equal(
            ["solution-alpha", "solution-zeta"],
            groups[1].Entries.Select(static entry => entry.SolutionIdentity));
    }

    [Fact]
    [Trait("Requirement", "MSC-32")]
    public void Group_NameCarriedByTwoSolutions_YieldsOneGroupWithSharedIdentityNotProven()
    {
        var orders = Solution(
            "solution-orders",
            deploymentUnits: [Named("fact-orders", "ordering-api", "facts/architecture.json", 0)]);
        var catalog = Solution(
            "solution-catalog",
            deploymentUnits: [Named("fact-catalog", "ordering-api", "facts/architecture.json", 0)]);

        var group = Assert.Single(GlobalComponentCatalog.Group([orders, catalog]));

        Assert.Equal("ordering-api", group.CanonicalName);
        Assert.Equal(NamedIdentityGrouping.NotProven, group.SharedIdentity);
        Assert.Equal(2, group.Entries.Length);
    }

    [Fact]
    [Trait("Requirement", "MSC-32")]
    public void Group_NameCarriedByOneSolution_YieldsGroupWithoutSharedIdentityClaim()
    {
        var orders = Solution(
            "solution-orders",
            components: [Named("fact-comp", "ordering-api", "facts/architecture.json", 0)]);

        var group = Assert.Single(GlobalComponentCatalog.Group([orders]));

        Assert.Equal("ordering-api", group.CanonicalName);
        Assert.Null(group.SharedIdentity);
        Assert.Equal("fact-comp", Assert.Single(group.Entries).FactId);
    }

    [Fact]
    [Trait("Requirement", "MSC-33")]
    public void Group_NeverMergesTwoIdentitiesIntoOne()
    {
        var orders = Solution(
            "solution-orders",
            components: [Named("fact-comp", "ordering-api", "facts/architecture.json", 0)],
            deploymentUnits: [Named("fact-du", "ordering-api", "facts/architecture.json", 1)]);
        var catalog = Solution(
            "solution-catalog",
            components: [Named("fact-other", "ordering-api", "facts/architecture.json", 0)]);

        SolutionContribution[] contributions = [orders, catalog];
        var groups = GlobalComponentCatalog.Group(contributions);
        var inputCount = contributions.Sum(static contribution =>
            contribution.Components.Length + contribution.DeploymentUnits.Length);
        var outputCount = groups.Sum(static group => group.Entries.Length);

        Assert.Equal(3, inputCount);
        Assert.Equal(inputCount, outputCount);
        Assert.Equal(3, Assert.Single(groups).Entries.Select(static entry => entry.FactId).Distinct(StringComparer.Ordinal).Count());
    }

    private static SolutionContribution Solution(
        string identity,
        ContributedNamedIdentity[]? components = null,
        ContributedNamedIdentity[]? deploymentUnits = null) =>
        new(identity, identity + ".slnx", "s-" + identity, [], [], [.. components ?? []], [.. deploymentUnits ?? []], []);

    private static ContributedNamedIdentity Named(string factId, string name, string artifactKey, int ordinal) =>
        new(factId, "Component", name, artifactKey, ordinal);
}
