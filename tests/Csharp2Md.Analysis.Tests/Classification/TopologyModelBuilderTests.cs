using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Topology;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class TopologyModelBuilderTests
{
    [Fact]
    [Trait("Requirement", "CDC-18")]
    public void Build_EmptyLedger_ReturnsEmptyModelWithoutThrowing()
    {
        var pipeline = Arrange();

        var model = TopologyModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Empty(model.Groups);
        Assert.Empty(model.Deployments);
        Assert.Empty(model.Inclusions);
        Assert.Empty(model.Unreached);
        Assert.Equal(0, model.Coverage.ApplicationsFound);
        Assert.Equal(0, model.Coverage.ProjectsGrouped);
    }

    [Fact]
    [Trait("Requirement", "CDC-09")]
    [Trait("Requirement", "CDC-13")]
    public void Build_Chain_ApplicationReachesTransitiveLibrary()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var mid = AddProject(pipeline, "Mid/Mid.csproj");
        var leaf = AddProject(pipeline, "Leaf/Leaf.csproj");
        AddOutputKind(pipeline, app, "application");
        AddOutputKind(pipeline, mid, "library");
        AddOutputKind(pipeline, leaf, "library");
        AddReference(pipeline, app, "Mid/Mid.csproj", ordinal: 2);
        AddReference(pipeline, mid, "Leaf/Leaf.csproj", ordinal: 2);

        var model = TopologyModelBuilder.Build(new ClassifierContext(pipeline));

        var group = Assert.Single(model.Groups);
        Assert.Equal("App/App.csproj", group.ComponentName);
        Assert.Equal(GroupingEvidence.Deployable, group.Evidence);
        Assert.Equal(
            new[] { app.Id, leaf.Id, mid.Id }.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(),
            group.Projects.ToArray());
        Assert.Empty(model.Unreached);
    }

    [Fact]
    [Trait("Requirement", "CDC-13")]
    public void Build_Diamond_SingleApplicationStillPrivateUse()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var left = AddProject(pipeline, "Left/Left.csproj");
        var right = AddProject(pipeline, "Right/Right.csproj");
        var shared = AddProject(pipeline, "Shared/Shared.csproj");
        AddOutputKind(pipeline, app, "application");
        AddOutputKind(pipeline, left, "library");
        AddOutputKind(pipeline, right, "library");
        AddOutputKind(pipeline, shared, "library");
        AddReference(pipeline, app, "Left/Left.csproj", ordinal: 2);
        AddReference(pipeline, app, "Right/Right.csproj", ordinal: 3);
        AddReference(pipeline, left, "Shared/Shared.csproj", ordinal: 2);
        AddReference(pipeline, right, "Shared/Shared.csproj", ordinal: 2);

        var model = TopologyModelBuilder.Build(new ClassifierContext(pipeline));

        var group = Assert.Single(model.Groups);
        Assert.Equal("App/App.csproj", group.ComponentName);
        Assert.Equal(4, group.Projects.Length);
        Assert.Contains(shared.Id, group.Projects.ToArray());
        Assert.DoesNotContain(model.Groups, candidate => candidate.ComponentName == "Shared/Shared.csproj");
    }

    [Fact]
    [Trait("Requirement", "CDC-13")]
    public void Build_Cycle_TerminatesAndGroupsPrivateUseLibraries()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var one = AddProject(pipeline, "One/One.csproj");
        var two = AddProject(pipeline, "Two/Two.csproj");
        AddOutputKind(pipeline, app, "application");
        AddOutputKind(pipeline, one, "library");
        AddOutputKind(pipeline, two, "library");
        AddReference(pipeline, app, "One/One.csproj", ordinal: 2);
        AddReference(pipeline, one, "Two/Two.csproj", ordinal: 2);
        AddReference(pipeline, two, "One/One.csproj", ordinal: 2);

        var model = TopologyModelBuilder.Build(new ClassifierContext(pipeline));

        var group = Assert.Single(model.Groups);
        Assert.Equal("App/App.csproj", group.ComponentName);
        Assert.Equal(3, group.Projects.Length);
        Assert.Empty(model.Unreached);
    }

    [Fact]
    [Trait("Requirement", "CDC-12")]
    [Trait("Requirement", "CDC-13")]
    public void Build_IsolatedLibrary_IsUnreachedWithOwnComponent()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var isolated = AddProject(pipeline, "Isolated/Isolated.csproj");
        AddOutputKind(pipeline, app, "application");
        AddOutputKind(pipeline, isolated, "library");

        var model = TopologyModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Equal(2, model.Groups.Length);
        var unreached = Assert.Single(model.Groups, group => group.Evidence is GroupingEvidence.Unreached);
        Assert.Equal("Isolated/Isolated.csproj", unreached.ComponentName);
        Assert.Equal(new[] { isolated.Id }, unreached.Projects.ToArray());
        Assert.Equal("Isolated/Isolated.csproj", Assert.Single(model.Unreached).ComponentName);
        Assert.Equal("App/App.csproj", Assert.Single(model.Deployments).Name);
    }

    [Fact]
    [Trait("Requirement", "CDC-09")]
    public void Build_IgnoresSymbolOwnedAndDocumentOwnedConfiguration()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        AddOutputKind(pipeline, app, "application");
        var document = Document.Create(app.Id, "App/appsettings.json");
        pipeline.Accumulator.AddFact(document);
        pipeline.Accumulator.AddObservation(
            Observe(document.Reference, 1, ("key", "Services:PaymentService")));
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::App.Host", "Run", 0, "global::System.Void"),
            app.Id,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        pipeline.Accumulator.AddObservation(
            Observe(symbol.Reference, 1, ("key", "Logging:Level")));

        var model = TopologyModelBuilder.Build(new ClassifierContext(pipeline));

        var group = Assert.Single(model.Groups);
        Assert.Equal("App/App.csproj", group.ComponentName);
        Assert.Equal(new[] { app.Id }, group.Projects.ToArray());
    }

    internal static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.Accumulator.AddFact(Solution.Create(SolutionId));
        return pipeline;
    }

    internal static SolutionId SolutionId =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "App.slnx");

    internal static Project AddProject(PipelineContext pipeline, string logicalPath)
    {
        var project = Project.Create(ProjectId.Create(SolutionId, logicalPath));
        pipeline.Accumulator.AddFact(project);
        return project;
    }

    internal static void AddOutputKind(PipelineContext pipeline, Project owner, string kind) =>
        pipeline.Accumulator.AddObservation(Observe(owner.Reference, 1, ("output-kind", kind)));

    internal static void AddReference(PipelineContext pipeline, Project owner, string referencedPath, int ordinal) =>
        pipeline.Accumulator.AddObservation(
            Observe(owner.Reference, ordinal, ("project-reference", referencedPath)));

    internal static Observation Observe(FactReference owner, int ordinal, params (string Key, string Value)[] entries) =>
        Observation.Create(
            owner,
            ObservationKind.Configuration,
            NormalizedPayload.Create(
                entries.Select(entry =>
                    new PayloadEntry(
                        entry.Key,
                        StructuralLiteral.Create(LiteralRole.ConfigurationKey, entry.Value, entry.Key)))),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "App/App.csproj", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Configured,
            new BindingDiagnostic("configured", "configured"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
