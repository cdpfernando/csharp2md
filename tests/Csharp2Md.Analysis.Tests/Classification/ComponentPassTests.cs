using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ComponentPassTests
{
    [Fact]
    [Trait("Requirement", "EBC-01")]
    [Trait("Requirement", "EBC-03")]
    [Trait("Requirement", "EBC-04")]
    public void Execute_ProjectWithRouteDeclarationCandidate_CreatesComponentNamedForLogicalPath()
    {
        var pipeline = CreatePipeline();
        var solution = Solution.Create(AcmeSolution);
        var project = Project.Create(OrdersProject);
        var action = CreateSymbol("GetOrderStatus", OrdersProject);
        pipeline.Accumulator.AddFact(solution);
        pipeline.Accumulator.AddFact(project);
        pipeline.Accumulator.AddFact(action);
        pipeline.Accumulator.AddObservation(CreateObservation(action.Reference, ObservationKind.RouteDeclaration, ordinal: 1));
        var context = new ClassifierContext(pipeline);

        var result = new ComponentPass().Execute(context, CancellationToken.None);

        var component = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>().ToArray());
        Assert.Equal(1, result.FactCount);
        Assert.Equal(0, result.RelationCount);
        Assert.Equal("Acme.Orders/Acme.Orders.csproj", component.Name);
        Assert.Equal(solution.Id, component.Solution);
        Assert.Equal(action.Reference, Assert.Single(component.Owners.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "EBC-01")]
    [Trait("Requirement", "EBC-03")]
    public void Execute_BaseTypeAttributeUsageAndMessageOperation_AreCandidateKinds()
    {
        var pipeline = CreatePipeline();
        var solution = Solution.Create(AcmeSolution);
        var project = Project.Create(OrdersProject);
        var controller = CreateSymbol("OrdersController", OrdersProject, kind: "namedtype", container: "global::Acme.Orders.Api");
        var attributed = CreateSymbol("GetOrderStatus", OrdersProject);
        var publisher = CreateSymbol("PlaceOrderAsync", OrdersProject);
        pipeline.Accumulator.AddFact(solution);
        pipeline.Accumulator.AddFact(project);
        pipeline.Accumulator.AddFact(controller);
        pipeline.Accumulator.AddFact(attributed);
        pipeline.Accumulator.AddFact(publisher);
        pipeline.Accumulator.AddObservation(CreateObservation(controller.Reference, ObservationKind.BaseType, ordinal: 1));
        pipeline.Accumulator.AddObservation(CreateObservation(attributed.Reference, ObservationKind.AttributeUsage, ordinal: 1));
        pipeline.Accumulator.AddObservation(CreateObservation(publisher.Reference, ObservationKind.MessageOperation, ordinal: 1));
        var context = new ClassifierContext(pipeline);

        new ComponentPass().Execute(context, CancellationToken.None);

        var component = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>().ToArray());
        Assert.Equal("Acme.Orders/Acme.Orders.csproj", component.Name);
        Assert.Equal(
            new[] { attributed.Reference, publisher.Reference, controller.Reference }
                .OrderBy(owner => owner.Id.Value, StringComparer.Ordinal)
                .ToArray(),
            component.Owners.ToArray());
    }

    [Fact]
    [Trait("Requirement", "EBC-02")]
    public void Execute_ProjectWithoutCandidateObservations_DoesNotCreateComponent()
    {
        var pipeline = CreatePipeline();
        var solution = Solution.Create(AcmeSolution);
        var orders = Project.Create(OrdersProject);
        var contracts = Project.Create(ContractsProject);
        var service = CreateSymbol("PlaceOrderAsync", OrdersProject);
        var dto = CreateSymbol("OrderPlaced", ContractsProject, kind: "namedtype", container: "global::Acme.Shared.Contracts");
        pipeline.Accumulator.AddFact(solution);
        pipeline.Accumulator.AddFact(orders);
        pipeline.Accumulator.AddFact(contracts);
        pipeline.Accumulator.AddFact(service);
        pipeline.Accumulator.AddFact(dto);
        pipeline.Accumulator.AddObservation(CreateObservation(service.Reference, ObservationKind.Invocation, ordinal: 1));
        var context = new ClassifierContext(pipeline);

        var result = new ComponentPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>());
    }

    [Fact]
    [Trait("Requirement", "EBC-02")]
    public void Execute_OnlyOneOfTwoProjectsHasCandidates_SkipsTheOther()
    {
        var pipeline = CreatePipeline();
        var solution = Solution.Create(AcmeSolution);
        var orders = Project.Create(OrdersProject);
        var contracts = Project.Create(ContractsProject);
        var action = CreateSymbol("GetOrderStatus", OrdersProject);
        var dto = CreateSymbol("OrderPlaced", ContractsProject, kind: "namedtype", container: "global::Acme.Shared.Contracts");
        pipeline.Accumulator.AddFact(solution);
        pipeline.Accumulator.AddFact(orders);
        pipeline.Accumulator.AddFact(contracts);
        pipeline.Accumulator.AddFact(action);
        pipeline.Accumulator.AddFact(dto);
        pipeline.Accumulator.AddObservation(CreateObservation(action.Reference, ObservationKind.RouteDeclaration, ordinal: 1));
        var context = new ClassifierContext(pipeline);

        new ComponentPass().Execute(context, CancellationToken.None);

        var component = Assert.Single(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>().ToArray());
        Assert.Equal("Acme.Orders/Acme.Orders.csproj", component.Name);
        Assert.DoesNotContain(
            pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>(),
            candidate => candidate.Name == "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
    }

    [Fact]
    [Trait("Requirement", "EBC-31")]
    public void Execute_ObservationOwnerNotAKnownSymbol_SkipsWithoutAbort()
    {
        var pipeline = CreatePipeline();
        var solution = Solution.Create(AcmeSolution);
        var project = Project.Create(OrdersProject);
        var orphanOwner = CreateSymbol("Missing", OrdersProject).Reference;
        pipeline.Accumulator.AddFact(solution);
        pipeline.Accumulator.AddFact(project);
        pipeline.Accumulator.AddObservation(CreateObservation(orphanOwner, ObservationKind.RouteDeclaration, ordinal: 1));
        var context = new ClassifierContext(pipeline);

        var result = new ComponentPass().Execute(context, CancellationToken.None);

        Assert.Equal(0, result.FactCount);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Facts.OfType<Component>());
        Assert.False(pipeline.Accumulator.StructuralCorruption);
    }

    private static PipelineContext CreatePipeline() =>
        new(new SwallowingSession(), "alpha.sln");

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static ProjectId ContractsProject =>
        ProjectId.Create(AcmeSolution, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");

    private static Symbol CreateSymbol(
        string metadataName,
        ProjectId project,
        string kind = "method",
        string container = "global::Acme.Orders.Api.OrdersController") =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(kind, container, metadataName, 0, "global::System.Void"),
            project,
            SymbolFacetSet.Create(kind == "method" ? [SymbolFacet.Callable] : []));

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
