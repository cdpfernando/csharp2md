using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.Publication;
using Csharp2Md.Core.PackageBuilding.Rendering;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class MarkdownRendererTests
{
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsComponents() => Assert.Contains("component:orders", Text("markdown/index.md"));
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsTopFanInAndOut() => Assert.Contains("fan-in 1, fan-out 1", Text("markdown/index.md"));
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsCycles() => Assert.Contains("cycle:orders", Text("markdown/index.md"));
    [Fact][Trait("Requirement", "NAV-02")] public void Render_SummaryListsFourJourneys() { var text=Text("markdown/index.md"); Assert.All(Write().Manifest.Journeys, journey => Assert.Contains($"[{journey.Kind}]({journey.EntryPath})", text)); }
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsOutgoing() => Assert.Contains("component:billing (Http)", Text(Page()));
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsIncoming() => Assert.Contains("component:orders (Http)", Text(Page("component:billing")));
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsMeasures() => Assert.Contains("fan-out: 1", Text(Page()));
    [Fact][Trait("Requirement", "NAV-03")] public void Render_EntityPageListsImpactAndGaps() { var text=Text(Page()); Assert.Contains("impact: component:billing at depth 1", text); Assert.Contains("unknown gaps: 1", text); }
    [Fact][Trait("Requirement", "NAV-04")] public void Render_SummaryUsesExistingRootLink() => Assert.Contains($"]({Page()})", Text("markdown/index.md"));
    [Fact][Trait("Requirement", "NAV-05")] public void Render_UsesMachineManifestRoots() => Assert.Equal(Write().Manifest.Roots.Length, Write().Markdown.Length - 1);
    [Fact][Trait("Requirement", "NAV-05")] public void Render_EscapesMarkdownNames() => Assert.Contains("\\[orders\\]", Text("markdown/index.md", special: true));
    [Fact][Trait("Requirement", "NAV-05")] public void Render_IsByteStable() { var first=Write(); var second=Write(); Assert.Equal(first.Markdown.Select(artifact => artifact.Payload), second.Markdown.Select(artifact => artifact.Payload)); }
    private static string Page(string entity="component:orders") => Write().Manifest.Roots.Single(root => root.DisplayName == entity).MarkdownPath;
    private static string Text(string path, bool special=false) => Encoding.UTF8.GetString((special ? Special() : Write()).Markdown.Single(artifact => artifact.Path.Value == path).Payload.AsSpan());
    private static (PackageManifest Manifest, ImmutableArray<PlannedArtifact> Markdown) Write() { var model=Model("component:orders"); var machine=MachineArtifactWriter.Write(model, false); return (machine.Manifest, MarkdownRenderer.Render(model, machine.Manifest)); }
    private static (PackageManifest Manifest, ImmutableArray<PlannedArtifact> Markdown) Special() { var model=Model("component:[orders]"); var machine=MachineArtifactWriter.Write(model, false); return (machine.Manifest, MarkdownRenderer.Render(model, machine.Manifest)); }
    private static RetrievalModel Model(string root) { var solution=CanonicalIdentity.CreateSolution("app", "src/App.sln"); var dependencies=ImmutableArray.Create(new AggregatedDependency(AggregationScope.Component,new EntityHandle(root),new EntityHandle("component:billing"),DependencyCategory.Http,DependencyNature.Direct,1,[],[],[]),new AggregatedDependency(AggregationScope.Component,new EntityHandle("component:billing"),new EntityHandle(root),DependencyCategory.Http,DependencyNature.Direct,1,[],[],[])); var measures=ImmutableArray.Create(new ScopeMeasures(AggregationScope.Component,new EntityHandle(root),1,1,0,[new CycleHandle("cycle:orders")],[new ImpactTarget(new EntityHandle("component:billing"),1)],new GapCounts(0,1,0))); return new RetrievalModel([new SolutionNavigation(solution,[new EntityHandle(root),new EntityHandle("component:billing")])],dependencies,measures,new NavigationIndexes("identity","roots","outgoing","incoming","contracts","persistence","evidence")); }
}
