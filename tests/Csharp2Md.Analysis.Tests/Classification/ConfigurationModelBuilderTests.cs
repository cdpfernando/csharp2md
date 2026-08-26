using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Configuration;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ConfigurationModelBuilderTests
{
    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_SingleDocumentKey_BindsToTheGroupingComponent()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        var key = Assert.Single(model.Keys);
        Assert.Equal("Services:PaymentService", key.KeyPath);
        Assert.Equal(KeyResolution.Literal, key.Resolution);
        Assert.Equal("https://payments.internal.acme.local:8443", key.Address);
        Assert.Equal(
            Component.Create(SolutionId, "Acme.Orders/Acme.Orders.csproj", OwnersOf(pipeline, project)).Reference,
            key.OwningComponent);
        Assert.Equal(document.Reference, key.Evidence.Owner);
        Assert.Equal(1, model.Coverage.KeysDeclared);
        Assert.Empty(model.Edges);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_TwoDocumentsDeclaringTheSameKey_ProducesTwoEntriesWithoutMerge()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var production = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var development = AddDocument(pipeline, project, "Acme.Orders/appsettings.Development.json");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, production, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);
        AddDeclaredKey(pipeline, development, "Services:PaymentService", "literal", "https://payments.dev.acme.local:8443", ordinal: 1);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Equal(2, model.Keys.Length);
        Assert.All(model.Keys, key => Assert.Equal("Services:PaymentService", key.KeyPath));
        Assert.Equal(
            new[] { production.Reference, development.Reference }.OrderBy(owner => owner.Id.Value, StringComparer.Ordinal),
            model.Keys.Select(key => key.Evidence.Owner));
        Assert.Equal(2, model.Coverage.KeysDeclared);
        Assert.Equal(2, model.Keys.Select(key => key.Evidence).Distinct().Count());
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_DocumentWhoseProjectHasNoComponent_SkipsWithDiagnostic()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Broken/Acme.Broken.csproj");
        var document = AddDocument(pipeline, project, "Acme.Broken/appsettings.json");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", address: null, ordinal: 1);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Empty(model.Keys);
        Assert.Equal(0, model.Coverage.KeysDeclared);
        var diagnostic = Assert.Single(pipeline.Accumulator.ToSnapshot().Diagnostics.ToArray());
        Assert.Equal(ConfigurationModelBuilder.UngroupedDocumentCode, diagnostic.Code);
        Assert.Equal(document.RelativePath, diagnostic.IdentityOrKey);
        Assert.Contains(document.RelativePath, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_ProjectOwnedAndSymbolOwnedConfiguration_AreIgnored()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        pipeline.Accumulator.AddObservation(
            Observe(project.Reference, 1, ("output-kind", "application")));
        var symbol = OwnersOf(pipeline, project)[0];
        pipeline.Accumulator.AddObservation(
            Observe(symbol, 1, ("key", "Logging:Level")));

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Empty(model.Keys);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.Accumulator.AddFact(Solution.Create(SolutionId));
        return pipeline;
    }

    private static SolutionId SolutionId =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static Project AddProject(PipelineContext pipeline, string logicalPath)
    {
        var project = Project.Create(ProjectId.Create(SolutionId, logicalPath));
        pipeline.Accumulator.AddFact(project);
        return project;
    }

    private static Document AddDocument(PipelineContext pipeline, Project project, string relativePath)
    {
        var document = Document.Create(project.Id, relativePath);
        pipeline.Accumulator.AddFact(document);
        return document;
    }

    private static void Group(PipelineContext pipeline, Project project, string componentName)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.Program", "Main", 0, "global::System.Void"),
            project.Id,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        pipeline.Accumulator.AddFact(Component.Create(SolutionId, componentName, [symbol.Reference]));
    }

    private static FactReference[] OwnersOf(PipelineContext pipeline, Project project) =>
        pipeline.Accumulator.ToSnapshot().Facts.OfType<Symbol>()
            .Where(symbol => symbol.OwningProject.Equals(project.Id))
            .Select(symbol => symbol.Reference)
            .ToArray();

    private static void AddDeclaredKey(
        PipelineContext pipeline,
        Document document,
        string key,
        string resolution,
        string? address,
        int ordinal)
    {
        var entries = new List<(string Key, string Value)>
        {
            ("key", key),
            ("resolution", resolution),
        };
        if (address is not null)
        {
            entries.Add(("address", address));
        }

        pipeline.Accumulator.AddObservation(Observe(document.Reference, ordinal, [.. entries]));
    }

    private static Observation Observe(FactReference owner, int ordinal, params (string Key, string Value)[] entries) =>
        Observation.Create(
            owner,
            ObservationKind.Configuration,
            NormalizedPayload.Create(
                entries.Select(entry =>
                    new PayloadEntry(
                        entry.Key,
                        StructuralLiteral.Create(LiteralRole.ConfigurationKey, entry.Value, entry.Key)))),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/appsettings.json", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Configured,
            new BindingDiagnostic("configured", "configured"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
