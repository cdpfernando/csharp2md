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
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class TopologyEmitterTests
{
    [Fact]
    [Trait("Requirement", "CDC-14")]
    public void Emit_Owners_AreSymbolsWithObservationsSortedByFactId()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var withObservation = AddSymbol(pipeline, app, "Host");
        var also = AddSymbol(pipeline, app, "Worker");
        AddObservation(pipeline, withObservation, 1);
        AddObservation(pipeline, also, 1);
        AddSymbol(pipeline, app, "Unused");
        var model = Deployable(app, Chain(pipeline, withObservation));
        var context = Context(pipeline);

        TopologyEmitter.Emit(model, context);

        var component = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>());
        Assert.Equal("App/App.csproj", component.Name);
        Assert.Equal(
            new[] { also.Reference, withObservation.Reference }
                .OrderBy(owner => owner.Id.Value, StringComparer.Ordinal)
                .ToArray(),
            component.Owners.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CDC-16")]
    public void Emit_SymbolOwningNoObservation_IsNotAnOwnerAndHasNoBelongsTo()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var unused = AddSymbol(pipeline, app, "Unused");
        var used = AddSymbol(pipeline, app, "Host");
        AddObservation(pipeline, used, 1);
        var model = Deployable(app, Chain(pipeline, used));
        var context = Context(pipeline);

        TopologyEmitter.Emit(model, context);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var component = Assert.Single(snapshot.Facts.OfType<Component>());
        Assert.DoesNotContain(unused.Reference, component.Owners.ToArray());
        Assert.DoesNotContain(
            snapshot.ConfirmedRelations,
            relation => relation.Kind is RelationKind.BelongsTo && relation.Source.Equals(unused.Reference));
    }

    [Fact]
    [Trait("Requirement", "CDC-15")]
    [Trait("Requirement", "CDC-17")]
    public void Emit_BelongsTo_IsSemanticDerivedFromTheSymbolsOwnObservations()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var host = AddSymbol(pipeline, app, "Host");
        var first = AddObservation(pipeline, host, 1);
        var second = AddObservation(pipeline, host, 2);
        var model = Deployable(app, EvidenceChain.Create([first.Identity]));
        var context = Context(pipeline);

        var result = TopologyEmitter.Emit(model, context);

        var belongs = pipeline.Accumulator.ToSnapshot().ConfirmedRelations
            .Where(relation => relation.Kind is RelationKind.BelongsTo)
            .ToArray();
        var relation = Assert.Single(belongs);
        Assert.Equal(host.Reference, relation.Source);
        Assert.Equal(EvidenceMethod.Semantic, MinimumBelongsTo);
        Assert.Equal("csharp2md.classifier.component-topology", relation.Classifier.Id);
        Assert.Equal(1, relation.Classifier.Version);
        Assert.Contains(first.Identity, relation.DerivedFrom.DerivedFrom.ToArray());
        Assert.Contains(second.Identity, relation.DerivedFrom.DerivedFrom.ToArray());
        Assert.Equal(1, belongs.Count(candidate => candidate.Source.Equals(host.Reference)));
        Assert.True(result.RelationCount >= 1);
    }

    [Fact]
    [Trait("Requirement", "CDC-20")]
    public void Emit_IncludedIn_IsConfiguredFromReachEvidence()
    {
        var pipeline = Arrange();
        var app = AddProject(pipeline, "App/App.csproj");
        var host = AddSymbol(pipeline, app, "Host");
        var outputKind = AddObservation(pipeline, app, 1, ("output-kind", "application"));
        AddObservation(pipeline, host, 1);
        var chain = EvidenceChain.Create([outputKind.Identity]);
        var model = Deployable(app, chain);
        var context = Context(pipeline);

        TopologyEmitter.Emit(model, context);

        var included = Assert.Single(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            relation => relation.Kind is RelationKind.IncludedIn);
        Assert.Equal(EvidenceMethod.Configured, MinimumIncludedIn);
        Assert.Equal(chain, included.DerivedFrom);
        Assert.Equal("DeploymentUnit", included.Target.FactType);
        Assert.Equal("Component", included.Source.FactType);
    }

    [Fact]
    [Trait("Requirement", "CDC-21")]
    public void Emit_SharedComponent_EmitsOneIncludedInPerReachingApplication()
    {
        var pipeline = Arrange();
        var orders = AddProject(pipeline, "Orders/Orders.csproj");
        var worker = AddProject(pipeline, "Worker/Worker.csproj");
        var contracts = AddProject(pipeline, "Contracts/Contracts.csproj");
        var dto = AddSymbol(pipeline, contracts, "OrderDto");
        AddObservation(pipeline, dto, 1);
        var ordersKind = AddObservation(pipeline, orders, 1, ("output-kind", "application"));
        var workerKind = AddObservation(pipeline, worker, 1, ("output-kind", "application"));
        var model = new TopologyModel(
            [
                new ComponentGroup("Orders/Orders.csproj", GroupingEvidence.Deployable, [orders.Id]),
                new ComponentGroup("Worker/Worker.csproj", GroupingEvidence.Deployable, [worker.Id]),
                new ComponentGroup("Contracts/Contracts.csproj", GroupingEvidence.Shared, [contracts.Id]),
            ],
            [
                new DeploymentNode("Orders/Orders.csproj", orders.Id),
                new DeploymentNode("Worker/Worker.csproj", worker.Id),
            ],
            [
                new InclusionEdge("Contracts/Contracts.csproj", "Orders/Orders.csproj", EvidenceChain.Create([ordersKind.Identity])),
                new InclusionEdge("Contracts/Contracts.csproj", "Worker/Worker.csproj", EvidenceChain.Create([workerKind.Identity])),
            ],
            [],
            new TopologyCoverage(3, 2, 0));
        var context = Context(pipeline);

        TopologyEmitter.Emit(model, context);

        var included = pipeline.Accumulator.ToSnapshot().ConfirmedRelations
            .Where(relation => relation.Kind is RelationKind.IncludedIn)
            .ToArray();
        Assert.Equal(2, included.Length);
        Assert.All(included, relation => Assert.Equal("Component", relation.Source.FactType));
        Assert.Equal(
            2,
            included.Select(relation => relation.Target.Id.Value).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("Requirement", "CDC-22")]
    public void Emit_UnreachedComponent_EmitsUnresolvedAndNoConfirmedIncludedIn()
    {
        var pipeline = Arrange();
        var lib = AddProject(pipeline, "Lib/Lib.csproj");
        var type = AddSymbol(pipeline, lib, "UnusedType");
        var outputKind = AddObservation(pipeline, lib, 1, ("output-kind", "library"));
        AddObservation(pipeline, type, 1);
        var chain = EvidenceChain.Create([outputKind.Identity]);
        var model = new TopologyModel(
            [new ComponentGroup("Lib/Lib.csproj", GroupingEvidence.Unreached, [lib.Id])],
            [],
            [],
            [new UnreachedComponent("Lib/Lib.csproj", chain)],
            new TopologyCoverage(1, 0, 1));
        var context = Context(pipeline);

        var result = TopologyEmitter.Emit(model, context);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        Assert.Equal(1, result.UnresolvedCount);
        var unresolved = Assert.Single(snapshot.Unresolved);
        Assert.Equal(RelationKind.IncludedIn, unresolved.Kind);
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.DoesNotContain(snapshot.ConfirmedRelations, relation => relation.Kind is RelationKind.IncludedIn);
        Assert.Single(snapshot.Facts.OfType<Component>());
        Assert.Empty(snapshot.Facts.OfType<DeploymentUnit>());
    }

    private static EvidenceMethod MinimumBelongsTo =>
        Csharp2Md.Domain.Registry.TaxonomyTables.Default.Relations
            .Single(relation => relation.Kind == RelationKind.BelongsTo)
            .MinimumEvidenceMethod;

    private static EvidenceMethod MinimumIncludedIn =>
        Csharp2Md.Domain.Registry.TaxonomyTables.Default.Relations
            .Single(relation => relation.Kind == RelationKind.IncludedIn)
            .MinimumEvidenceMethod;

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln")
        {
            AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")],
        };
        pipeline.Accumulator.AddFact(Solution.Create(SolutionId));
        return pipeline;
    }

    private static ClassifierContext Context(PipelineContext pipeline) => new(pipeline);

    private static SolutionId SolutionId =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "App.slnx");

    private static Project AddProject(PipelineContext pipeline, string logicalPath)
    {
        var project = Project.Create(ProjectId.Create(SolutionId, logicalPath));
        pipeline.Accumulator.AddFact(project);
        return project;
    }

    private static Symbol AddSymbol(PipelineContext pipeline, Project project, string metadata) =>
        AddNamed(pipeline, project, metadata);

    private static Symbol AddNamed(PipelineContext pipeline, Project project, string metadata)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("namedtype", "global::App", metadata, 0, "global::App." + metadata),
            project.Id,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Observation AddObservation(
        PipelineContext pipeline,
        IFact owner,
        int ordinal,
        params (string Key, string Value)[] entries)
    {
        var observation = Observation.Create(
            owner.Reference,
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
        pipeline.Accumulator.AddObservation(observation);
        return observation;
    }

    private static EvidenceChain Chain(PipelineContext pipeline, Symbol owner) =>
        EvidenceChain.Create(
            pipeline.Accumulator.ToSnapshot().Observations
                .Where(observation => observation.Identity.Owner.Equals(owner.Reference))
                .Select(observation => observation.Identity));

    private static TopologyModel Deployable(Project app, EvidenceChain evidence) =>
        new(
            [new ComponentGroup(LogicalPath(app), GroupingEvidence.Deployable, [app.Id])],
            [new DeploymentNode(LogicalPath(app), app.Id)],
            [new InclusionEdge(LogicalPath(app), LogicalPath(app), evidence)],
            [],
            new TopologyCoverage(1, 1, 0));

    private static string LogicalPath(Project project)
    {
        const string marker = ";path=";
        var id = project.Id.Value;
        var start = id.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = id.IndexOf(';', start);
        var encoded = end < 0 ? id[start..] : id[start..end];
        return Uri.UnescapeDataString(encoded);
    }
}
