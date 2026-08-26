using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ClassifierContextTests
{
    [Fact]
    [Trait("Requirement", "EBC-28")]
    public void Constructor_BindsAccumulatorAndVariantsFromPipelineContext()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        var variant = AnalysisVariantId.Create("net10.0", "Debug", [], "local");
        pipeline.AnalysisVariants = [variant];
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));

        var context = new ClassifierContext(pipeline);

        Assert.Same(pipeline.Accumulator, context.Accumulator);
        Assert.Equal(variant, Assert.Single(context.AnalysisVariants.ToArray()));
        Assert.Equal(AcmeSolution, context.SolutionId);
    }

    [Fact]
    [Trait("Requirement", "EBC-28")]
    public void FactsByType_ReturnsOnlyFactsOfThatType()
    {
        var pipeline = CreatePipeline();
        var solution = Solution.Create(AcmeSolution);
        var project = Project.Create(OrdersProject);
        pipeline.Accumulator.AddFact(solution);
        pipeline.Accumulator.AddFact(project);

        var context = new ClassifierContext(pipeline);

        Assert.Equal(solution, Assert.Single(context.FactsByType<Solution>().ToArray()));
        Assert.Equal(project, Assert.Single(context.FactsByType<Project>().ToArray()));
        Assert.Empty(context.FactsByType<Symbol>());
        Assert.Equal(2, context.Facts.Length);
    }

    [Fact]
    [Trait("Requirement", "EBC-28")]
    public void ObservationsByKind_ReturnsOnlyMatchingKind()
    {
        var pipeline = CreatePipeline();
        var owner = Solution.Create(AcmeSolution).Reference;
        var baseType = CreateObservation(owner, ObservationKind.BaseType, ordinal: 1);
        var invocation = CreateObservation(owner, ObservationKind.Invocation, ordinal: 2);
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddObservation(baseType);
        pipeline.Accumulator.AddObservation(invocation);

        var context = new ClassifierContext(pipeline);

        Assert.Equal(baseType, Assert.Single(context.ObservationsByKind(ObservationKind.BaseType).ToArray()));
        Assert.Equal(invocation, Assert.Single(context.ObservationsByKind(ObservationKind.Invocation).ToArray()));
        Assert.Empty(context.ObservationsByKind(ObservationKind.RouteDeclaration));
    }

    [Fact]
    [Trait("Requirement", "EBC-28")]
    public void ObservationsByOwner_ReturnsOnlyMatchingOwner()
    {
        var pipeline = CreatePipeline();
        var solution = Solution.Create(AcmeSolution);
        var project = Project.Create(OrdersProject);
        var ownedBySolution = CreateObservation(solution.Reference, ObservationKind.BaseType, ordinal: 1);
        var ownedByProject = CreateObservation(project.Reference, ObservationKind.BaseType, ordinal: 1);
        pipeline.Accumulator.AddFact(solution);
        pipeline.Accumulator.AddFact(project);
        pipeline.Accumulator.AddObservation(ownedBySolution);
        pipeline.Accumulator.AddObservation(ownedByProject);

        var context = new ClassifierContext(pipeline);

        Assert.Equal(ownedBySolution, Assert.Single(context.ObservationsByOwner(solution.Reference).ToArray()));
        Assert.Equal(ownedByProject, Assert.Single(context.ObservationsByOwner(project.Reference).ToArray()));
        Assert.Empty(context.ObservationsByOwner(Symbol.Create(RunSignature, OrdersProject, SymbolFacetSet.Create([])).Reference));
    }

    [Fact]
    [Trait("Requirement", "EBC-28")]
    public void SymbolsBySignatureKey_IndexesByProjectIdAndSignature()
    {
        var pipeline = CreatePipeline();
        var ordersSymbol = Symbol.Create(RunSignature, OrdersProject, SymbolFacetSet.Create([SymbolFacet.Callable]));
        var contractsSymbol = Symbol.Create(RunSignature, ContractsProject, SymbolFacetSet.Create([]));
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(ordersSymbol);
        pipeline.Accumulator.AddFact(contractsSymbol);

        var context = new ClassifierContext(pipeline);
        var index = context.SymbolsBySignatureKey();

        Assert.Equal(ordersSymbol, index[ClassifierContext.SignatureKey(OrdersProject, RunSignature)]);
        Assert.Equal(contractsSymbol, index[ClassifierContext.SignatureKey(ContractsProject, RunSignature)]);
        Assert.NotEqual(
            ClassifierContext.SignatureKey(OrdersProject, RunSignature),
            ClassifierContext.SignatureKey(ContractsProject, RunSignature));
        Assert.Equal(2, index.Count);
    }

    [Fact]
    [Trait("Requirement", "EBC-28")]
    public void Refresh_RebuildsIndexesFromCurrentAccumulator()
    {
        var pipeline = CreatePipeline();
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        var context = new ClassifierContext(pipeline);
        Assert.Empty(context.FactsByType<Project>());
        Assert.Empty(context.ObservationsByKind(ObservationKind.BaseType));
        Assert.Empty(context.SymbolsBySignatureKey());

        var project = Project.Create(OrdersProject);
        var symbol = Symbol.Create(RunSignature, OrdersProject, SymbolFacetSet.Create([]));
        var observation = CreateObservation(symbol.Reference, ObservationKind.BaseType, ordinal: 1);
        pipeline.Accumulator.AddFact(project);
        pipeline.Accumulator.AddFact(symbol);
        pipeline.Accumulator.AddObservation(observation);

        Assert.Empty(context.FactsByType<Project>());
        Assert.Empty(context.ObservationsByKind(ObservationKind.BaseType));

        context.Refresh();

        Assert.Equal(project, Assert.Single(context.FactsByType<Project>().ToArray()));
        Assert.Equal(observation, Assert.Single(context.ObservationsByKind(ObservationKind.BaseType).ToArray()));
        Assert.Equal(observation, Assert.Single(context.ObservationsByOwner(symbol.Reference).ToArray()));
        Assert.Equal(symbol, context.SymbolsBySignatureKey()[ClassifierContext.SignatureKey(OrdersProject, RunSignature)]);
    }

    private static PipelineContext CreatePipeline() =>
        new(new SwallowingSession(), "alpha.sln");

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static ProjectId ContractsProject =>
        ProjectId.Create(AcmeSolution, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");

    private static CanonicalSymbolSignature RunSignature =>
        CanonicalSymbolSignature.Create("method", "global::Acme.Orders.OrderService", "PlaceOrderAsync", 0, "global::System.Threading.Tasks.Task");

    private static Observation CreateObservation(FactReference owner, ObservationKind kind, int ordinal) =>
        Observation.Create(
            owner,
            kind,
            NormalizedPayload.Create([]),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Orders/Program.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
