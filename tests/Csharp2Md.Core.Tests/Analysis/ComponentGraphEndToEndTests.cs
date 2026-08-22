using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// spec.md's P1 Independent Tests for the component-graph feature, restated verbatim against a real
/// default-mode (syntax-only, untrusted) <see cref="AnalysisEngine.AnalyzeAsync"/> run over the full
/// <c>fixtures/SyntheticSolution</c> tree - all 5 projects, including <c>Acme.Broken</c> and
/// <c>Acme.DoesNotExist</c>, unlike the 3-project subset <c>RelationResolverEndToEndTests</c> and
/// <c>DataAccessDiscoveryEndToEndTests</c> use - mirroring their run-once-assert-many structure.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ComponentGraphEndToEndTests(ComponentGraphEndToEndFixture fixture)
    : IClassFixture<ComponentGraphEndToEndFixture>
{
    private static readonly string[] ExpectedProjects =
        ["Acme.Orders", "Acme.Payments", "Acme.Shared.Contracts", "Acme.Broken", "Acme.DoesNotExist"];

    // COMP-01/COMP-03: one ComponentFact per analysed ProjectFact - exactly 5, each named individually by
    // its own project id, none shared or duplicated.
    [Fact]
    public void ComponentFragment_HoldsExactlyFiveComponentFactsNamedIndividually()
    {
        Assert.Equal(5, fixture.Components.Length);
        Assert.All(fixture.Components, static component => Assert.Single(component.ProjectIds));

        foreach (var project in ExpectedProjects)
        {
            Assert.Single(fixture.Components, component => component.ProjectIds[0].Contains(project, StringComparison.Ordinal));
        }
    }

    // COMP-04: resolution is mirrored from the owning ProjectFact, not hardcoded - a live default-mode run
    // reports every project as syntactic.
    [Fact]
    public void ComponentFragment_EachComponentCarriesSyntacticResolution()
    {
        Assert.All(fixture.Components, static component => Assert.Equal("syntactic", component.Header.Resolution));
    }

    // COMP-29: Acme.DoesNotExist's .csproj is missing on disk, so it has zero documents and zero relations
    // - it still gets a component and a components.md entry, and it never touches the diagram because no
    // edge reaches it.
    [Fact]
    public void AcmeDoesNotExist_HasAComponentAppearsInComponentsMdAndNeverInDependenciesMermaid()
    {
        Assert.Contains(fixture.Components, component => component.ProjectIds[0].Contains("Acme.DoesNotExist", StringComparison.Ordinal));
        Assert.Contains("Acme.DoesNotExist", fixture.ComponentIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme.DoesNotExist", fixture.Mermaid, StringComparison.Ordinal);
    }

    // COMP-05: components.md lists every one of the 5 components and nothing else - bounded by counting
    // "- id:" entries rather than only checking presence, so an extra stray entry would be caught too.
    [Fact]
    public void ComponentsMd_ListsAllFiveAndNothingElse()
    {
        foreach (var project in ExpectedProjects)
        {
            Assert.Contains(project, fixture.ComponentIndex, StringComparison.Ordinal);
        }

        var idLines = fixture.ComponentIndex.Split('\n').Count(static line => line.TrimStart().StartsWith("- id: `", StringComparison.Ordinal));
        Assert.Equal(5, idLines);
    }
}

public sealed class ComponentGraphEndToEndFixture : IAsyncLifetime
{
    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-cg-e2e-{Guid.NewGuid():N}");

    public ImmutableArray<ComponentFactJson> Components { get; private set; }

    public string ComponentIndex { get; private set; } = string.Empty;

    public string Mermaid { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // fixtures/SyntheticSolution has no top-level solution, so pointing AnalysisRequest at its root
        // auto-discovers every project directory as its own service - all 5 projects, including
        // Acme.Broken and Acme.DoesNotExist - the same approach AnalysisEngineTests' own
        // ProjectReachedBySeveralPaths tests already use.
        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(TestPaths.SyntheticSolution("."), Output, topic: "acme-component-graph-e2e", domain: "system-design").Request);
        var result = await new AnalysisEngine().AnalyzeAsync(request);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
        }

        var raw = TopicLayout.RawRoot(Output);
        var manifest = Deserialize(Path.Combine(raw, "facts", "manifest.json"), AggregateJsonContext.Default.FactualManifest);
        var componentFragment = manifest.Fragments.Single(fragment => fragment.FactId.StartsWith("id1:component", StringComparison.Ordinal));
        var componentFragmentPath = Path.Combine(raw, componentFragment.Reference.Replace('/', Path.DirectorySeparatorChar));
        Components = FactualJsonSerializer.Deserialize(File.ReadAllBytes(componentFragmentPath)).Components;

        ComponentIndex = File.ReadAllText(Path.Combine(raw, "codebase", "components.md"));
        Mermaid = File.ReadAllText(Path.Combine(raw, "dependencies.mmd"));
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(Output))
        {
            Directory.Delete(Output, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static T Deserialize<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Deserialize(File.ReadAllBytes(path), typeInfo)
            ?? throw new InvalidOperationException($"{path} deserialized to null.");
}
