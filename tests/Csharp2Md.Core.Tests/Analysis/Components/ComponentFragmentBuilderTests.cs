using Csharp2Md.Core.Analysis.Components;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Analysis.Components;

public sealed class ComponentFragmentBuilderTests
{
    // COMP-01: one component per project, ComponentKind "project", ProjectIds holding exactly the one
    // owning project id and no other.
    [Fact]
    public void Build_OneProject_YieldsOneComponentFactOwningExactlyThatProjectAndNoOther()
    {
        var project = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Syntactic);

        var result = ComponentFragmentBuilder.Build([project], KnownIds(project), FactValidator.Validate);

        var component = Assert.Single(result.Components);
        Assert.Equal("project", component.ComponentKind);
        Assert.Equal(project.ProjectId, Assert.Single(component.ProjectIds));
    }

    // COMP-04: resolution is copied from the source project, never hardcoded - proven with a Syntactic
    // project.
    [Fact]
    public void Build_SyntacticProject_MirrorsSyntacticResolutionOnTheComponentHeader()
    {
        var project = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Syntactic);

        var result = ComponentFragmentBuilder.Build([project], KnownIds(project), FactValidator.Validate);

        Assert.Equal(FactResolution.Syntactic, Assert.Single(result.Components).Header.Resolution);
    }

    // COMP-04: and with an Exact project, so the mirroring is proven both ways rather than assumed from
    // one sample.
    [Fact]
    public void Build_ExactProject_MirrorsExactResolutionOnTheComponentHeader()
    {
        var project = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Exact);

        var result = ComponentFragmentBuilder.Build([project], KnownIds(project), FactValidator.Validate);

        Assert.Equal(FactResolution.Exact, Assert.Single(result.Components).Header.Resolution);
    }

    // COMP-04: provenance names the component producer and evidence stays empty - a .csproj has no line
    // range to point at.
    [Fact]
    public void Build_OneProject_CarriesTheComponentsProvenanceAndNoEvidence()
    {
        var project = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Syntactic);

        var result = ComponentFragmentBuilder.Build([project], KnownIds(project), FactValidator.Validate);

        var component = Assert.Single(result.Components);
        var provenance = Assert.Single(component.Header.Provenance);
        Assert.Equal("csharp2md.components", provenance.EngineId);
        Assert.Equal("1", provenance.EngineVersion);
        Assert.Empty(component.Header.Evidence);
    }

    // COMP-08: two components sharing one identity fail the run structurally rather than one being
    // silently dropped.
    [Fact]
    public void Build_TwoProjectsSharingOneProjectIdentity_ThrowsInvalidOperationException()
    {
        var project = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Syntactic);
        var duplicate = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Syntactic);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ComponentFragmentBuilder.Build([project, duplicate], KnownIds(project), FactValidator.Validate));

        Assert.Contains("Duplicate component identity", exception.Message, StringComparison.Ordinal);
    }

    // COMP-06 (via the builder that feeds it): a run analysing zero projects emits no ComponentFact.
    [Fact]
    public void Build_NoProjects_ReturnsEmptyResultWithNoFragmentAndNoDiagnostics()
    {
        var result = ComponentFragmentBuilder.Build([], new HashSet<FactId>(), FactValidator.Validate);

        Assert.Same(ComponentFragmentResult.Empty, result);
        Assert.Null(result.Fragment);
        Assert.Empty(result.Components);
        Assert.Empty(result.Diagnostics);
    }

    // Components is exposed on the result separately from the validated Fragment, both carrying the
    // same two components.
    [Fact]
    public void Build_TwoProjects_ExposesComponentsSeparatelyFromTheValidatedFragment()
    {
        var orders = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Syntactic);
        var payments = Project("src/Payments/Payments.csproj", "Payments", FactResolution.Syntactic);

        var result = ComponentFragmentBuilder.Build([orders, payments], KnownIds(orders, payments), FactValidator.Validate);

        Assert.Equal(2, result.Components.Length);
        Assert.NotNull(result.Fragment);
        Assert.Equal(
            result.Components.Select(static component => component.ComponentId.Value).Order(StringComparer.Ordinal),
            result.Fragment!.Facts.Select(static fact => fact.Header.Id.Value).Order(StringComparer.Ordinal));
    }

    // A component's only reference (its project id) must be supplied via knownFactIds because the
    // project fact itself lives outside this fragment - proven by triggering C2M-FV-002 when it is
    // withheld, the same failure COMP-31's "unknown project id" edge case describes.
    [Fact]
    public void Build_ProjectIdNotSuppliedAsKnown_FailsValidationWithMissingReference()
    {
        var project = Project("src/Orders/Orders.csproj", "Orders", FactResolution.Syntactic);

        var result = ComponentFragmentBuilder.Build([project], new HashSet<FactId>(), FactValidator.Validate);

        Assert.Null(result.Fragment);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "C2M-FV-002");
    }

    private static HashSet<FactId> KnownIds(params ProjectFact[] projects) =>
        projects.Select(static project => project.ProjectId.ToFactId()).ToHashSet();

    private static ProjectFact Project(string relativePath, string name, FactResolution resolution)
    {
        var id = ProjectFactId.Create(relativePath);
        return new ProjectFact(
            FactHeader.Create(id.ToFactId(), FactKind.Project, resolution, [new FactProvenance("test", "1")]),
            id,
            name,
            relativePath,
            [],
            []);
    }
}
