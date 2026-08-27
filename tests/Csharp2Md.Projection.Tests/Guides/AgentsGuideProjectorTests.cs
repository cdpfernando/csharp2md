using System.Text;
using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Projection.Guides;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Guides;

public sealed class AgentsGuideProjectorTests
{
    [Fact]
    [Trait("Requirement", "RP-38")]
    public void Project_ExplainsManifestAsTheEntryPoint()
    {
        var text = GuideText(AgentsGuideProjector.Project(CatalogProjectionFactory.ViewOf()));

        Assert.Contains("manifest", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("entry point", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "RP-38")]
    public void Project_ExplainsProofStateAxes()
    {
        var text = GuideText(AgentsGuideProjector.Project(CatalogProjectionFactory.ViewOf()));

        Assert.Contains("evidence method", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolution", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frontier", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("confidence", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "RP-38")]
    public void Project_ExplainsHowToRetrieveSource()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateSolutionFact());
        var text = GuideText(AgentsGuideProjector.Project(view));
        var slots = view.Slots.Select(static slot => slot.CanonicalKey).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("source", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("locator", text, StringComparison.OrdinalIgnoreCase);
        var named = ArtifactKeys(text);
        Assert.NotEmpty(named);
        Assert.All(named, key => Assert.True(slots.Contains(key), key));
    }

    [Fact]
    [Trait("Requirement", "RP-38")]
    public void Project_ExplainsMarkdownIsAProjectionAndNeverAuthority()
    {
        var text = GuideText(AgentsGuideProjector.Project(CatalogProjectionFactory.ViewOf()));

        Assert.Contains("Markdown", text, StringComparison.Ordinal);
        Assert.Contains("projection", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never authority", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "RP-39")]
    public void Project_DoesNotEnumerateFactTypesObservationKindsFacetAxesOrRelationTriples()
    {
        var text = GuideText(AgentsGuideProjector.Project(CatalogProjectionFactory.ViewOf()));
        var tables = TaxonomyTables.Default;

        foreach (var factType in tables.FactTypes.Select(static descriptor => descriptor.Name))
        {
            Assert.DoesNotMatch("\\b" + Regex.Escape(factType) + "\\b", text);
        }

        foreach (var kind in tables.ObservationKinds.Select(static descriptor => descriptor.WireName))
        {
            Assert.DoesNotContain(kind, text, StringComparison.Ordinal);
        }

        foreach (var axis in tables.FacetAxes.Select(static descriptor => descriptor.Name))
        {
            Assert.DoesNotContain(axis, text, StringComparison.Ordinal);
        }

        foreach (var relation in tables.Relations)
        {
            foreach (var triple in relation.Triples)
            {
                Assert.DoesNotContain(triple.SourceFactType + " -[" + relation.Kind + "]-> " + triple.TargetFactType, text, StringComparison.Ordinal);
                Assert.DoesNotContain(triple.SourceFactType + " -[" + relation.WireName + "]-> " + triple.TargetFactType, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-38")]
    [Trait("Requirement", "RP-40")]
    public void Project_AgentsGuide_IsListedInManifestAndContainsNoAbsolutePath()
    {
        var store = new InMemoryTransactionalStore(new PackageProjector());
        var session = store.Open("s-test", new EmptySourceReader());
        session.Stage(FactualSnapshot.Empty);
        var publication = session.Commit();
        var text = GuideText(AgentsGuideProjector.Project(CatalogProjectionFactory.ViewOf()));

        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            publication.ArtifactsInPublicationOrder
                .Single(static fragment => fragment.CanonicalKey == "manifest.json")
                .Payload
                .AsSpan());
        Assert.Contains(manifest.Artifacts, entry => entry.Path == AgentsGuideProjector.Key);
        Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
        Assert.DoesNotContain(":/", text, StringComparison.Ordinal);
        foreach (var key in ArtifactKeys(text))
        {
            Assert.False(Path.IsPathRooted(key), key);
        }
    }

    private static string GuideText(ImmutableArray<StagedFragment> fragments) =>
        Encoding.UTF8.GetString(Assert.Single(fragments).Payload.AsSpan());

    private static ImmutableArray<string> ArtifactKeys(string text) =>
        [.. Regex.Matches(text, "`([^`]+)`")
            .Select(static match => match.Groups[1].Value)
            .Where(static key => key.Contains('/', StringComparison.Ordinal)
                || key.EndsWith(".json", StringComparison.Ordinal)
                || key.EndsWith(".md", StringComparison.Ordinal))];
}
