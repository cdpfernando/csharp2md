using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class MarkdownRendererTests
{
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsComponents() => Assert.Contains("component:orders", Text("markdown/index.md"));
    // NAV-02 asks the summary for componentes AND Deployment Units; the section is even titled after both.
    // Every case above builds only `component:` roots, so the Deployment Unit half was never asserted.
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsDeploymentUnitsNotOnlyComponents()
    {
        var text = Text("markdown/index.md");
        Assert.Contains("## Components and Deployment Units", text, StringComparison.Ordinal);
        Assert.Contains("deployment:orders-api", text, StringComparison.Ordinal);
        var link = System.Text.RegularExpressions.Regex.Match(text, @"- \[deployment:orders-api\]\(([^)]+)\)");
        Assert.True(link.Success, "the deployment unit row is not a Markdown link");
        var written = Write().Markdown.Select(artifact => artifact.Path.Value).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(Resolve("markdown/index.md", link.Groups[1].Value), written);
    }

    // Resolves a Markdown link the way a reader following it would, so the assertion proves the row reaches a
    // written artifact rather than merely looking like a link.
    private static string Resolve(string from, string target)
    {
        var segments = new List<string>(from.Split('/')[..^1]);
        foreach (var part in target.Split('/'))
        {
            if (part == "..") segments.RemoveAt(segments.Count - 1);
            else if (part != ".") segments.Add(part);
        }

        return string.Join('/', segments);
    }

    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsTopFanInAndOut() => Assert.Contains("fan-in 1, fan-out 1", Text("markdown/index.md"));
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsCycles() => Assert.Contains("cycle:orders", Text("markdown/index.md"));
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsFourJourneys() { var text=Text("markdown/index.md"); var solution=Assert.Single(Write().Manifest.Solutions); Assert.All(solution.Journeys, journey => Assert.Contains($"{journey.Kind} via {journey.EntryIndex}", text)); }
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsOutgoing() => Assert.Contains("- [component:billing](0.md) (Http)", Text(Page()));
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsIncoming() => Assert.Contains("- [component:orders](1.md) (Http)", Text(Page("component:billing")));
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsMeasures() => Assert.Contains("fan-out: 1", Text(Page()));
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsImpactAndGaps() { var text=Text(Page()); Assert.Contains("- impact: [component:billing](0.md) at depth 1", text); Assert.Contains("unknown gaps: 1", text); }
    [Fact][Trait("Requirement", "NAV-04")] public void Render_SummaryUsesExistingRootLink() => Assert.Contains($"](../{Page()})", Text("markdown/index.md"));
    [Fact][Trait("Requirement", "NAV-05")] public void Render_UsesMachineManifestRoots() => Assert.Equal(Assert.Single(Write().Manifest.Solutions).Roots.Count, Write().Markdown.Length - 1);
    [Fact][Trait("Requirement", "NAV-05")] public void Render_EscapesMarkdownNames() => Assert.Contains("\\[orders\\]", Text("markdown/index.md", special: true));
    [Fact][Trait("Requirement", "NAV-05")] public void Render_IsByteStable() { var first=Write(); var second=Write(); Assert.Equal(first.Markdown.Select(artifact => artifact.Payload), second.Markdown.Select(artifact => artifact.Payload)); }
    [Fact][Trait("Requirement", "NAV-03")] public void Render_CitedDocumentGetsItsOwnPage()
    {
        var index = DocumentIndex();
        var cited = Assert.Single(index.Documents, document => document.DisplayName == "document:src/Billing.cs");
        Assert.Contains(index.MarkdownPath(cited.Handle), Cited().Markdown.Select(artifact => artifact.Path.Value));
    }

    // Only the document-scoped edge is a citation. "document:src/Uncited.cs" is named by a component-scoped
    // edge instead, so no kept evidence cites it as a document and PKG-06 keeps it out of the page set.
    [Fact][Trait("Requirement", "PKG-06")] public void Render_UncitedDocumentGetsNoPage()
    {
        var index = DocumentIndex();
        Assert.Equal(["document:src/Billing.cs", "document:src/Orders.cs"], index.Documents.Select(document => document.DisplayName));
        Assert.DoesNotContain(Cited().Markdown.Select(artifact => artifact.Path.Value), path => path.EndsWith("/documents/2.md", StringComparison.Ordinal));
    }

    [Fact][Trait("Requirement", "NAV-03")] public void Render_RowLinksToAWrittenPage() => Assert.Contains("- impact: [document:src/Billing.cs](../documents/0.md) at depth 1", CitedText(CitedRootPage()));

    [Fact][Trait("Requirement", "NAV-03")] public void Render_RowWithoutAPageStaysPlainText()
    {
        var text = CitedText(CitedRootPage());
        Assert.Contains("- document:src/Uncited.cs (Http)", text);
        Assert.DoesNotContain("[document:src/Uncited.cs]", text);
    }

    [Fact][Trait("Requirement", "NAV-03")] public void Render_EveryLinkResolvesToAWrittenArtifact()
    {
        var markdown = Cited().Markdown;
        var written = markdown.Select(artifact => artifact.Path.Value).ToHashSet(StringComparer.Ordinal);
        var links = markdown.SelectMany(artifact => Links(artifact.Path.Value, Encoding.UTF8.GetString(artifact.Payload.AsSpan()))).ToArray();
        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.Contains(link, written));
    }

    [Fact][Trait("Requirement", "NAV-03")] public void Render_SummaryReachesEveryDocumentPage()
    {
        var index = DocumentIndex();
        var summary = CitedText("markdown/index.md");
        Assert.Contains("## Retained documents", summary);
        Assert.All(index.Documents, document => Assert.Contains($"- [{document.DisplayName}](../{index.MarkdownPath(document.Handle)})", summary));
    }

    private static IEnumerable<string> Links(string page, string text)
    {
        var directory = page[..(page.LastIndexOf('/') + 1)];
        foreach (var line in text.Split('\n'))
        {
            var open = line.IndexOf("](", StringComparison.Ordinal);
            if (open < 0) continue;
            var close = line.IndexOf(')', open + 2);
            yield return Normalize(directory + line[(open + 2)..close]);
        }
    }

    private static string Normalize(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment == "..") segments.RemoveAt(segments.Count - 1);
            else segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    private static DocumentsIndexData DocumentIndex()
    {
        var model = CitedModel();
        var machine = MachineArtifactWriter.Write(model, false);
        return MachineArtifactWriter.BuildDocuments(Assert.Single(model.Solutions).Dependencies, Assert.Single(machine.Manifest.Solutions).Id);
    }

    private static string CitedRootPage()
    {
        var model = CitedModel();
        var machine = MachineArtifactWriter.Write(model, false);
        var index = MachineArtifactWriter.BuildRoots(Assert.Single(model.Solutions), Assert.Single(machine.Manifest.Solutions).Id);
        return index.MarkdownPath(Assert.Single(index.Roots, root => root.DisplayName == "component:orders").Handle);
    }

    private static string CitedText(string path) => Encoding.UTF8.GetString(Cited().Markdown.Single(artifact => artifact.Path.Value == path).Payload.AsSpan());

    private static (PackageManifest Manifest, ImmutableArray<PlannedArtifact> Markdown) Cited() { var model = CitedModel(); var machine = MachineArtifactWriter.Write(model, false); return (machine.Manifest, MarkdownRenderer.Render(model, machine.Manifest)); }

    private static RetrievalModel CitedModel() =>
        new([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("app", "src/App.sln"),
            [new EntityHandle("component:orders")],
            [
                new AggregatedDependency(AggregationScope.Document, new EntityHandle("document:src/Orders.cs"), new EntityHandle("document:src/Billing.cs"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], []),
                new AggregatedDependency(AggregationScope.Component, new EntityHandle("component:orders"), new EntityHandle("document:src/Uncited.cs"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], []),
            ],
            [new ScopeMeasures(AggregationScope.Component, new EntityHandle("component:orders"), 0, 1, 0, [], [new ImpactTarget(new EntityHandle("document:src/Billing.cs"), 1)], new GapCounts(0, 0, 0))])]);

    private static string Page(string entity="component:orders")
    {
        var model = Model("component:orders");
        var machine = MachineArtifactWriter.Write(model, false);
        var index = MachineArtifactWriter.BuildRoots(Assert.Single(model.Solutions), Assert.Single(machine.Manifest.Solutions).Id);
        return index.MarkdownPath(index.Roots.Single(root => root.DisplayName == entity).Handle);
    }
    private static string Text(string path, bool special=false) => Encoding.UTF8.GetString((special ? Special() : Write()).Markdown.Single(artifact => artifact.Path.Value == path).Payload.AsSpan());
    private static (PackageManifest Manifest, ImmutableArray<PlannedArtifact> Markdown) Write() { var model=Model("component:orders"); var machine=MachineArtifactWriter.Write(model, false); return (machine.Manifest, MarkdownRenderer.Render(model, machine.Manifest)); }
    private static (PackageManifest Manifest, ImmutableArray<PlannedArtifact> Markdown) Special() { var model=Model("component:[orders]"); var machine=MachineArtifactWriter.Write(model, false); return (machine.Manifest, MarkdownRenderer.Render(model, machine.Manifest)); }
    private static RetrievalModel Model(string root)
    {
        var solution = CanonicalIdentity.CreateSolution("app", "src/App.sln");
        var dependencies = ImmutableArray.Create(
            new AggregatedDependency(AggregationScope.Component, new EntityHandle(root), new EntityHandle("component:billing"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], []),
            new AggregatedDependency(AggregationScope.Component, new EntityHandle("component:billing"), new EntityHandle(root), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], []));
        var measures = ImmutableArray.Create(new ScopeMeasures(
            AggregationScope.Component,
            new EntityHandle(root),
            1,
            1,
            0,
            [new CycleHandle("cycle:orders")],
            [new ImpactTarget(new EntityHandle("component:billing"), 1)],
            new GapCounts(0, 1, 0)));
        return new RetrievalModel([new SolutionRetrievalModel(
            solution,
            [new EntityHandle(root), new EntityHandle("component:billing"), new EntityHandle("deployment:orders-api")],
            dependencies,
            measures)]);
    }
}
