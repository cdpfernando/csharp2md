using System.Text;
using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection.Guides;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Guides;

public sealed class RetrievalGuideProjectorTests
{
    private static readonly string[] Scenarios =
    [
        "locate an entry point, operation, contract, symbol or data field",
        "open the canonical fact and direct relations",
        "follow `executes`, `implements-operation` and `invokes`",
        "inspect `uses-contract`, `accesses-data`, `operates-on` and `targets` effects",
        "open complete callable bodies through source locators",
        "inspect candidates and open frontiers separately",
        "stop on terminal effects, cycles, unsupported capabilities or a declared reading budget",
    ];

    [Fact]
    [Trait("Requirement", "RP-37")]
    public void Project_DocumentsAllSevenRetrievalScenarios()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"));

        var text = GuideText(RetrievalGuideProjector.Project(view));

        Assert.Equal(7, Scenarios.Length);
        foreach (var scenario in Scenarios)
        {
            Assert.Contains(scenario, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    public void Project_EachScenarioNamesAnArtifactKeyPresentInSlots()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"));
        var slots = view.Slots.Select(static slot => slot.CanonicalKey).ToHashSet(StringComparer.Ordinal);

        var text = GuideText(RetrievalGuideProjector.Project(view));
        var sections = ScenarioBodies(text);

        Assert.Equal(7, sections.Length);
        foreach (var section in sections)
        {
            var named = ArtifactKeys(section);
            Assert.NotEmpty(named);
            Assert.Contains(named, key => slots.Contains(key));
        }
    }

    [Fact]
    [Trait("Requirement", "RP-40")]
    public void Project_RetrievalGuide_IsListedInManifest()
    {
        var store = new InMemoryTransactionalStore(new PackageProjector());
        var session = store.Open("s-test", new EmptySourceReader());
        session.Stage(new FactualSnapshot(
            [CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api")],
            [],
            [],
            [],
            [],
            []));
        var publication = session.Commit();

        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            publication.ArtifactsInPublicationOrder
                .Single(static fragment => fragment.CanonicalKey == "manifest.json")
                .Payload
                .AsSpan());
        Assert.Contains(manifest.Artifacts, entry => entry.Path == RetrievalGuideProjector.Key);
        Assert.Contains(
            publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey == RetrievalGuideProjector.Key);
    }

    [Fact]
    [Trait("Requirement", "RP-40")]
    public void Project_RetrievalGuide_ContainsNoAbsolutePath()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateComponent("Orders.Api"));

        var text = GuideText(RetrievalGuideProjector.Project(view));

        Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
        Assert.DoesNotContain(":/", text, StringComparison.Ordinal);
        foreach (var key in ArtifactKeys(text))
        {
            Assert.False(Path.IsPathRooted(key), key);
            Assert.False(key.StartsWith("/", StringComparison.Ordinal), key);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    [Trait("Requirement", "RP-40")]
    public void Project_DoesNotNameASlotKeyAbsentFromThePublication()
    {
        var view = CatalogProjectionFactory.ViewOf();
        var slots = view.Slots.Select(static slot => slot.CanonicalKey).ToHashSet(StringComparer.Ordinal);

        var named = ArtifactKeys(GuideText(RetrievalGuideProjector.Project(view)));

        Assert.DoesNotContain("relations/candidates.json", named);
        Assert.DoesNotContain("facts/architecture.json", named);
        Assert.All(named, key => Assert.True(slots.Contains(key), key));
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    public void Project_EmptyArchitecture_StillPublishesRetrievalGuide()
    {
        var view = CatalogProjectionFactory.ViewOf();

        var fragments = RetrievalGuideProjector.Project(view);

        Assert.Equal(RetrievalGuideProjector.Key, Assert.Single(fragments).CanonicalKey);
        Assert.Empty(MarkdownProjector.Project(view));
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    [Trait("Requirement", "RP-38")]
    public void PackageProjector_NoArchitectureFacts_PublishesGuidesWithoutMarkdownPages()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateSolutionFact(),
            CatalogProjectionFactory.CreateProjectFact("src/Acme.Orders/Acme.Orders.csproj"),
            CatalogProjectionFactory.CreateDocumentFact(
                "src/Acme.Orders/Acme.Orders.csproj",
                "src/Acme.Orders/Program.cs"));
        var reader = new EmptySourceReader();

        var fragments = new PackageProjector().Project(view, reader);

        Assert.DoesNotContain(
            fragments,
            fragment => fragment.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal));
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == RetrievalGuideProjector.Key);
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == AgentsGuideProjector.Key);
    }

    [Fact]
    [Trait("Requirement", "RP-37")]
    public void PackageProjector_ComposesRetrievalGuideAfterMarkdown()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"));
        var reader = new EmptySourceReader();

        var composed = new PackageProjector().Project(view, reader);
        var pages = MarkdownProjector.Project(view);
        var guide = RetrievalGuideProjector.Project(view);

        Assert.Equal(RetrievalGuideProjector.Key, composed[^2].CanonicalKey);
        Assert.Equal(pages.Length + 1, composed.Count(static fragment =>
            fragment.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal)
            || fragment.CanonicalKey == RetrievalGuideProjector.Key));
        Assert.Equal(guide[0].CanonicalKey, composed[^2].CanonicalKey);
        Assert.Equal(
            Encoding.UTF8.GetString(guide[0].Payload.AsSpan()),
            Encoding.UTF8.GetString(composed[^2].Payload.AsSpan()));
    }

    private static string GuideText(ImmutableArray<StagedFragment> fragments) =>
        Encoding.UTF8.GetString(Assert.Single(fragments).Payload.AsSpan());

    private static string[] ScenarioBodies(string text)
    {
        var matches = Regex.Matches(text, @"^## \d+\. .+$", RegexOptions.Multiline);
        Assert.Equal(7, matches.Count);
        var bodies = new string[7];
        for (var index = 0; index < matches.Count; index++)
        {
            var start = matches[index].Index;
            var end = index + 1 < matches.Count ? matches[index + 1].Index : text.Length;
            bodies[index] = text[start..end];
        }

        return bodies;
    }

    private static ImmutableArray<string> ArtifactKeys(string text) =>
        [.. Regex.Matches(text, "`([^`]+)`")
            .Select(static match => match.Groups[1].Value)
            .Where(static key => key.Contains('/', StringComparison.Ordinal)
                || key.EndsWith(".json", StringComparison.Ordinal)
                || key.EndsWith(".md", StringComparison.Ordinal))];
}
