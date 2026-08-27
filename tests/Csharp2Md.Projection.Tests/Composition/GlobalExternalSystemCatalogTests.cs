using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Tests.Composition;

public sealed class GlobalExternalSystemCatalogTests
{
    [Fact]
    [Trait("Requirement", "MSC-34")]
    public void Group_OutputShape_MatchesNamedIdentityGroupingHelper()
    {
        var orders = Solution(
            "solution-orders",
            Named("fact-stripe", "stripe-gateway", "facts/architecture.json", 1));
        var catalog = Solution(
            "solution-catalog",
            Named("fact-tax", "tax-service", "facts/architecture.json", 2));
        SolutionContribution[] contributions = [orders, catalog];

        var expected = NamedIdentityGrouping.Group(
            contributions.SelectMany(static contribution =>
                contribution.ExternalSystems.Select(identity => (contribution.SolutionIdentity, identity))));
        var actual = GlobalExternalSystemCatalog.Group(contributions);

        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].CanonicalName, actual[index].CanonicalName);
            Assert.Equal(expected[index].SharedIdentity, actual[index].SharedIdentity);
            Assert.Equal(
                expected[index].Entries.Select(static entry => (entry.FactId, entry.SolutionIdentity, entry.ArtifactKey, entry.Ordinal)),
                actual[index].Entries.Select(static entry => (entry.FactId, entry.SolutionIdentity, entry.ArtifactKey, entry.Ordinal)));
        }

        var source = File.ReadAllText(
            Path.Combine(
                ProjectionTestPaths.RepoRoot,
                "src",
                "Csharp2Md.Projection",
                "Composition",
                "GlobalExternalSystemCatalog.cs"));
        Assert.Contains("NamedIdentityGrouping.Group", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-34")]
    public void Group_SameExternalSystemNameInTwoSolutions_YieldsOneGroupWithSharedIdentityNotProven()
    {
        var orders = Solution(
            "solution-orders",
            Named("fact-orders", "stripe-gateway", "facts/architecture.json", 0));
        var payments = Solution(
            "solution-payments",
            Named("fact-payments", "stripe-gateway", "facts/architecture.json", 0));

        var group = Assert.Single(GlobalExternalSystemCatalog.Group([orders, payments]));

        Assert.Equal("stripe-gateway", group.CanonicalName);
        Assert.Equal(NamedIdentityGrouping.NotProven, group.SharedIdentity);
        Assert.Equal(2, group.Entries.Length);
        Assert.Contains(group.Entries, entry => entry.FactId == "fact-orders" && entry.SolutionIdentity == "solution-orders");
        Assert.Contains(group.Entries, entry => entry.FactId == "fact-payments" && entry.SolutionIdentity == "solution-payments");
    }

    private static SolutionContribution Solution(string identity, params ContributedNamedIdentity[] externalSystems) =>
        new(identity, identity + ".slnx", "s-" + identity, [], [], [], [], [.. externalSystems]);

    private static ContributedNamedIdentity Named(string factId, string name, string artifactKey, int ordinal) =>
        new(factId, "ExternalSystem", name, artifactKey, ordinal);
}
