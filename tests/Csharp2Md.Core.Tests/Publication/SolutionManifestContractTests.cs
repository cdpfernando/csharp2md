using System.Text;
using System.Text.Json;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class SolutionManifestContractTests
{
    [Fact]
    [Trait("Requirement", "PKG-01")]
    [Trait("Requirement", "STO-04")]
    public void PackageManifest_GroupsRootsIndexesAndJourneysUnderTypedSolutionId()
    {
        var manifest = Manifest();

        var solution = Assert.Single(manifest.Solutions);
        Assert.IsType<SolutionId>(solution.Id);
        Assert.Equal("sol_0123456789abcdef", solution.Id.Value);
        Assert.Equal("src/App.sln", solution.LogicalRelativePath);
        Assert.Equal("solutions/sol_0123456789abcdef/indexes/roots.json", solution.Roots.EntryPath);
        Assert.Equal(1, solution.Roots.Count);
        Assert.Equal(8, solution.Indexes.Length);
        Assert.Equal(4, solution.Journeys.Length);
        Assert.DoesNotContain(typeof(PackageManifest).GetProperties(), property => property.Name is "Roots" or "Indexes" or "Journeys");
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void NavigationIndexKind_IsTheClosedSemanticSet()
    {
        Assert.Equal(
            [
                NavigationIndexKind.Identity,
                NavigationIndexKind.Roots,
                NavigationIndexKind.Outgoing,
                NavigationIndexKind.Incoming,
                NavigationIndexKind.Contracts,
                NavigationIndexKind.Persistence,
                NavigationIndexKind.Evidence,
                NavigationIndexKind.Measures,
            ],
            Enum.GetValues<NavigationIndexKind>());
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void CanonicalJson_EntryPathAndEntryIndex_AreSemanticSnakeCaseStrings()
    {
        var json = CanonicalJson.Write(Manifest());
        using var document = JsonDocument.Parse(json.ToArray());
        var solution = document.RootElement.GetProperty("solutions")[0];
        var identity = solution.GetProperty("indexes").EnumerateArray()
            .Single(index => index.GetProperty("kind").GetString() == "identity");
        var followFlow = solution.GetProperty("journeys").EnumerateArray()
            .Single(journey => journey.GetProperty("kind").GetString() == "follow_flow");

        Assert.Equal(JsonValueKind.String, solution.GetProperty("id").ValueKind);
        Assert.Equal("sol_0123456789abcdef", solution.GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.String, identity.GetProperty("kind").ValueKind);
        Assert.Equal("solutions/sol_0123456789abcdef/indexes/identity.json", identity.GetProperty("entry_path").GetString());
        Assert.Equal(JsonValueKind.String, followFlow.GetProperty("kind").ValueKind);
        Assert.Equal(JsonValueKind.String, followFlow.GetProperty("entry_index").ValueKind);
        Assert.Equal("outgoing", followFlow.GetProperty("entry_index").GetString());
        var text = Encoding.UTF8.GetString(json.AsSpan());
        Assert.DoesNotContain("\"kind\": 0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"entry_index\": 2", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    public void SolutionManifestEntry_DuplicateIndexKind_IsRejected()
    {
        var indexes = Indexes().Add(Index(NavigationIndexKind.Identity));

        var exception = Assert.Throws<ArgumentException>(() => Solution(indexes));

        Assert.Equal("indexes", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "PUB-03")]
    public void SolutionManifestEntry_MissingRequiredIndexKind_IsRejected()
    {
        var indexes = Indexes().Where(index => index.Kind != NavigationIndexKind.Measures).ToImmutableArray();

        var exception = Assert.Throws<ArgumentException>(() => Solution(indexes));

        Assert.Equal("indexes", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("solution:app")]
    [InlineData("sol_0123456789abcdew")]
    [InlineData("sol_0123456789abcde")]
    [Trait("Requirement", "STO-01")]
    public void SolutionId_NonCanonicalGrammar_IsRejected(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SolutionId(value));
        Assert.Equal("value", exception.ParamName);
    }

    private static PackageManifest Manifest() =>
        new(
            PackageManifest.TokenEstimatorName,
            PackageManifest.TokenDivisorValue,
            includeTests: false,
            [Solution(Indexes())]);

    private static SolutionManifestEntry Solution(ImmutableArray<IndexManifestEntry> indexes) =>
        new(
            new SolutionId("sol_0123456789abcdef"),
            "src/App.sln",
            new RootsManifestEntry("solutions/sol_0123456789abcdef/indexes/roots.json", 1),
            indexes,
            Journeys());

    private static ImmutableArray<IndexManifestEntry> Indexes() =>
        Enum.GetValues<NavigationIndexKind>().Select(Index).ToImmutableArray();

    private static IndexManifestEntry Index(NavigationIndexKind kind) =>
        new(kind, $"solutions/sol_0123456789abcdef/indexes/{SnakeCase(kind)}.json");

    private static ImmutableArray<JourneyManifestEntry> Journeys() =>
        [
            new(JourneyKind.Locate, NavigationIndexKind.Roots),
            new(JourneyKind.FollowFlow, NavigationIndexKind.Outgoing),
            new(JourneyKind.ReverseImpact, NavigationIndexKind.Incoming),
            new(JourneyKind.EvidenceDisposition, NavigationIndexKind.Evidence),
        ];

    private static string SnakeCase(NavigationIndexKind kind) => kind.ToString().ToLowerInvariant();
}
