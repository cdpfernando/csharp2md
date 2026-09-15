using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ExecutesPassTests
{
    [Fact]
    [Trait("Requirement", "CLLF-03")]
    public void EntryPointWithKnownSymbol_ProducesExecutesRelation()
    {
        var pipeline = Arrange();
        var action = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var component = AddComponent(pipeline, [action.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(action.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(CreateObservation(action.Reference, ObservationKind.RouteDeclaration, 1));
        var context = new ClassifierContext(pipeline);

        var result = new ExecutesPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(1, result.RelationCount);
        Assert.Equal(RelationKind.Executes, relation.Kind);
        Assert.Equal(action.Reference, relation.Target);
        Assert.Equal(ExecutesPass.Identity, relation.Classifier);
        Assert.Equal(pipeline.AnalysisVariants, relation.AnalysisVariants);
        Assert.Contains(relation.DerivedFrom.DerivedFrom, identity => identity.Kind is ObservationKind.RouteDeclaration);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "CLLF-03")]
    public void EntryPointWithMissingSymbol_ProducesDiagnosticNotRelation()
    {
        var pipeline = Arrange();
        var missing = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.Api.OrdersController", "MissingAction", 0, "global::System.Void"),
            OrdersProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        var component = AddComponent(pipeline, []);
        var entry = EntryPoint.Create(missing.Reference, component.Reference);
        pipeline.Accumulator.AddFact(entry);
        var context = new ClassifierContext(pipeline);

        var result = new ExecutesPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.RelationCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        var diagnostic = Assert.Single(pipeline.Accumulator.ToSnapshot().Diagnostics.ToArray());
        Assert.Equal("executes-pass", diagnostic.Code);
        Assert.Contains(entry.Reference.Id.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(missing.Reference.Id.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(entry.Reference.Id.Value, diagnostic.IdentityOrKey);
    }

    [Fact]
    [Trait("Requirement", "CLLF-03")]
    public void MultipleEntryPoints_EachProducesOneExecutes()
    {
        var pipeline = Arrange();
        var first = AddMethod(pipeline, "GetOrderStatus", "global::Acme.Orders.Api.OrdersController", OrdersProject);
        var second = AddMethod(pipeline, "HandleAsync", "global::Acme.Orders.Events.OrderPlacedEventHandler", OrdersProject);
        var component = AddComponent(pipeline, [first.Reference, second.Reference]);
        pipeline.Accumulator.AddFact(EntryPoint.Create(first.Reference, component.Reference));
        pipeline.Accumulator.AddFact(EntryPoint.Create(second.Reference, component.Reference));
        pipeline.Accumulator.AddObservation(CreateObservation(first.Reference, ObservationKind.RouteDeclaration, 1));
        pipeline.Accumulator.AddObservation(CreateObservation(second.Reference, ObservationKind.MessageOperation, 1));
        var context = new ClassifierContext(pipeline);

        var result = new ExecutesPass().Execute(context, CancellationToken.None);

        Assert.Equal(2, result.RelationCount);
        var relations = pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray();
        Assert.Equal(2, relations.Length);
        Assert.All(relations, relation => Assert.Equal(RelationKind.Executes, relation.Kind));
        Assert.Contains(relations, relation => relation.Target.Equals(first.Reference));
        Assert.Contains(relations, relation => relation.Target.Equals(second.Reference));
        Assert.Equal(2, relations.Select(relation => relation.Source.Id.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "CLLF-03")]
    public void Identity_IsExecutesClassifierVersionOne()
    {
        Assert.Equal("csharp2md.classifier.executes", ExecutesPass.Identity.Id);
        Assert.Equal(1, ExecutesPass.Identity.Version);
        Assert.Equal("Executes", new ExecutesPass().Name);
    }

    [Fact]
    [Trait("Requirement", "CLLF-03")]
    public async Task AnalyzeAsync_AcmeOrders_ControllerAndHandlerEntryPointsProduceExecutes()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var stages = PipelineStages.CreateDefault().SetItem(
            3,
            new ClassificationAndPromotionStage(
            [
                new ComponentPass(),
                new EntryPointPass(),
                new BoundaryPass(),
                new ContractPass(),
                new RelationPass(),
                new ExecutesPass(),
            ]));
        var result = await new AnalysisEngine(store, stages).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var architecture = ShardedFactsReader.Read<ArchitectureFactsShard>(
            publication.ArtifactsInPublicationOrder, "facts/architecture.json");
        var executes = CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "relations/confirmed/executes.json").Payload.AsSpan());

        var controllerEntry = Assert.Single(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && entry.Symbol.Id.Contains("OrdersController", StringComparison.Ordinal));
        Assert.Contains(
            executes,
            relation => relation.Kind == "executes"
                && relation.Source.Id == controllerEntry.Identity.Id
                && relation.Target.Id == controllerEntry.Symbol.Id);

        var handlerEntry = Assert.Single(
            architecture.EntryPoints,
            entry => entry.Symbol.Id.Contains("HandleAsync", StringComparison.Ordinal)
                && entry.Symbol.Id.Contains("OrderPlacedEventHandler", StringComparison.Ordinal));
        Assert.Contains(
            executes,
            relation => relation.Kind == "executes"
                && relation.Source.Id == handlerEntry.Identity.Id
                && relation.Target.Id == handlerEntry.Symbol.Id);

        foreach (var entry in architecture.EntryPoints.Where(entry =>
            entry.Symbol.Id.Contains("OrdersController", StringComparison.Ordinal)))
        {
            Assert.Contains(
                executes,
                relation => relation.Kind == "executes"
                    && relation.Source.Id == entry.Identity.Id
                    && relation.Target.Id == entry.Symbol.Id);
        }
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(Project.Create(OrdersProject));
        return pipeline;
    }

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static Symbol AddMethod(PipelineContext pipeline, string metadata, string container, ProjectId project)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", container, metadata, 0, "global::System.Void"),
            project,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Component AddComponent(PipelineContext pipeline, IEnumerable<FactReference> owners)
    {
        var component = Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", owners);
        pipeline.Accumulator.AddFact(component);
        return component;
    }

    private static Observation CreateObservation(FactReference owner, ObservationKind kind, int ordinal) =>
        Observation.Create(
            owner,
            kind,
            NormalizedPayload.Create([]),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Orders/Api/OrdersController.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
