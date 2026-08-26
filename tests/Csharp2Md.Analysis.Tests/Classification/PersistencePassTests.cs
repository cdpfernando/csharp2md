using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Classification.Persistence;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class PersistencePassTests
{
    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Name_IsPersistence()
    {
        Assert.Equal("Persistence", new PersistencePass().Name);
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void Execute_IsBuilderThenEmitterWithNoClassificationLogicOfItsOwn()
    {
        var source = File.ReadAllText(PersistencePassPath());

        Assert.Contains("PersistenceModelBuilder.Build(context, cancellationToken)", source, StringComparison.Ordinal);
        Assert.Contains("PersistenceEmitter.Emit(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DataStore.Create", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmedRelation.Create", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    public void CreateDefault_RegistersPersistencePassAfterContractPassAndBeforeRelationPass()
    {
        var source = File.ReadAllText(PipelineStagesPath());
        var contract = source.IndexOf("new ContractPass()", StringComparison.Ordinal);
        var persistence = source.IndexOf("new PersistencePass()", StringComparison.Ordinal);
        var relation = source.IndexOf("new RelationPass()", StringComparison.Ordinal);

        Assert.True(contract >= 0, "CreateDefault must register ContractPass.");
        Assert.True(persistence >= 0, "CreateDefault must register PersistencePass.");
        Assert.True(relation >= 0, "CreateDefault must register RelationPass.");
        Assert.True(
            contract < persistence && persistence < relation,
            "CreateDefault pass order must place PersistencePass after ContractPass and before RelationPass.");
    }

    [Fact]
    [Trait("Requirement", "PK-44")]
    public async Task ExecuteAsync_StageResultCountsIncludePersistenceFactsAndRelations()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(Project.Create(OrdersProject));
        AddNamedType(pipeline, "global::Acme.Orders.Data.Order");
        AddDbSetProperty(pipeline, "global::Acme.Orders.Data.OrderDbContext", "Orders", "global::Acme.Orders.Data.Order");
        var reader = AddMethod(pipeline, "GetOrder", "global::Acme.Orders.Data.OrderQueries");
        pipeline.Accumulator.AddObservation(
            Observation.Create(
                reader.Reference,
                ObservationKind.DataAccess,
                NormalizedPayload.Create(
                [
                    new PayloadEntry("operation", StructuralLiteral.Create(LiteralRole.ProtocolName, "read", "operation")),
                    new PayloadEntry("entity-type", StructuralLiteral.Create(LiteralRole.ProtocolName, "global::Acme.Orders.Data.Order", "entity-type")),
                ]),
                1,
                new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/Data/OrderQueries.cs", new SourceSpan(1, 1, 1, 8)),
                EvidenceMethod.Semantic,
                new BindingDiagnostic("bound", "bound"),
                DocumentHash.Create(new string('a', 64)),
                new ExtractorVersion(1)));

        var result = await new ClassificationAndPromotionStage([new PersistencePass()])
            .ExecuteAsync(pipeline, CancellationToken.None);

        var snapshot = pipeline.Accumulator.ToSnapshot();
        var persistenceFacts = snapshot.Facts.Count(fact => fact.Family is FactFamily.Persistence);
        var persistenceRelations = snapshot.ConfirmedRelations.Count(relation =>
            relation.Kind is RelationKind.AccessesData or RelationKind.OperatesOn or RelationKind.MapsTo);
        Assert.True(persistenceFacts > 0, $"Expected persistence facts, found {persistenceFacts}.");
        Assert.True(persistenceRelations > 0, $"Expected persistence relations, found {persistenceRelations}.");
        Assert.Equal(persistenceFacts, result.FactCount);
        Assert.Equal(persistenceRelations, result.RelationCount);
        Assert.Single(snapshot.Facts.OfType<DataStore>());
    }

    [Fact]
    [Trait("Requirement", "PK-43")]
    [Trait("Requirement", "PK-44")]
    public async Task AnalyzeAsync_NoOpPassAlongsideRealPasses_DoesNotInterfereWithPersistenceOutput()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var baseline = await AnalyzeAsync(solutionPath, PipelineStages.CreateDefault());
        var order = new List<string>();
        var composed = await AnalyzeAsync(
            solutionPath,
            PipelineStages.CreateDefault().SetItem(
                3,
                new ClassificationAndPromotionStage(
                [
                    new RecordingPass(new ComponentPass(), order),
                    new RecordingPass(new EntryPointPass(), order),
                    new RecordingPass(new BoundaryPass(), order),
                    new RecordingPass(new ContractPass(), order),
                    new RecordingPass(new PersistencePass(), order),
                    new RecordingPass(new RelationPass(), order),
                    new NoOpPass(order),
                ])));

        Assert.Equal(
            ["Components", "Entry points", "Boundaries", "Contracts", "Persistence", "Relations", NoOpPass.PassName],
            order);

        var baselinePersistence = ReadPersistence(baseline.Publication);
        var composedPersistence = ReadPersistence(composed.Publication);
        Assert.NotEmpty(baselinePersistence.DataStores);
        Assert.Equal(baselinePersistence.DataStores.Select(dto => dto.Identity.Id), composedPersistence.DataStores.Select(dto => dto.Identity.Id));
        Assert.Equal(baselinePersistence.DataObjects.Select(dto => dto.Identity.Id), composedPersistence.DataObjects.Select(dto => dto.Identity.Id));
        Assert.Equal(baseline.Outcome.Stages[3].FactCount, composed.Outcome.Stages[3].FactCount);
        Assert.Equal(baseline.Outcome.Stages[3].RelationCount, composed.Outcome.Stages[3].RelationCount);
        Assert.True(
            baseline.Outcome.Stages[3].FactCount >= baselinePersistence.DataStores.Length
                + baselinePersistence.DataObjects.Length
                + baselinePersistence.DataFields.Length
                + baselinePersistence.DataOperations.Length,
            $"Classification fact count {baseline.Outcome.Stages[3].FactCount} did not include persistence output.");
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAsync(
        string solutionPath,
        ImmutableArray<IPipelineStage> stages)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store, stages).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static PersistenceFactsShard ReadPersistence(CommittedPublication publication) =>
        CanonicalJson.Read<PersistenceFactsShard>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "facts/persistence.json").Payload.AsSpan());

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static string PersistencePassPath() =>
        Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Classification",
            "Passes",
            "PersistencePass.cs");

    private static string PipelineStagesPath() =>
        Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Analysis",
            "Pipeline",
            "PipelineStages.cs");

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static void AddDbSetProperty(PipelineContext pipeline, string container, string metadata, string entityTypeFqn) =>
        pipeline.Accumulator.AddFact(
            Symbol.Create(
                CanonicalSymbolSignature.Create(
                    "property",
                    container,
                    metadata,
                    0,
                    PersistenceModelBuilder.DbSetTypePrefix + entityTypeFqn + ">"),
                OrdersProject,
                SymbolFacetSet.Create([])));

    private static Symbol AddMethod(PipelineContext pipeline, string metadata, string container)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", container, metadata, 0, "global::System.Void"),
            OrdersProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static Symbol AddNamedType(PipelineContext pipeline, string fullyQualifiedName)
    {
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create(
                "namedtype",
                fullyQualifiedName[..lastDot],
                fullyQualifiedName[(lastDot + 1)..],
                0,
                fullyQualifiedName),
            OrdersProject,
            SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private sealed class RecordingPass(IClassifierPass inner, List<string> order) : IClassifierPass
    {
        public string Name => inner.Name;

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            order.Add(Name);
            return inner.Execute(context, cancellationToken);
        }
    }

    private sealed class NoOpPass(List<string> order) : IClassifierPass
    {
        internal const string PassName = "No-op";

        public string Name => PassName;

        public ClassifierPassResult Execute(ClassifierContext context, CancellationToken cancellationToken)
        {
            order.Add(Name);
            return new ClassifierPassResult(0, 0, 0, 0);
        }
    }
}
