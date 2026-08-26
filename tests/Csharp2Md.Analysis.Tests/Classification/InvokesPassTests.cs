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
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class InvokesPassTests
{
    [Fact]
    [Trait("Requirement", "CLLF-01")]
    public void Execute_BoundMethodTargetInSameProject_CreatesConfirmedInvokesWithSemanticEvidence()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var target = AddMethod(pipeline, "Authorize", "global::Acme.Shared.Contracts.PaymentClient", OrdersProject);
        AddBoundInvocation(pipeline, source, target, ordinal: 1);
        var context = new ClassifierContext(pipeline);

        var result = new InvokesPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(1, result.RelationCount);
        Assert.Equal(RelationKind.Invokes, relation.Kind);
        Assert.Equal(source.Reference, relation.Source);
        Assert.Equal(target.Reference, relation.Target);
        Assert.Equal(InvokesPass.Identity, relation.Classifier);
        Assert.Contains(relation.DerivedFrom.DerivedFrom, identity => identity.Kind is ObservationKind.Invocation);
        Assert.Equal(pipeline.AnalysisVariants, relation.AnalysisVariants);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Candidates);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
    }

    [Fact]
    [Trait("Requirement", "CLLF-02")]
    public void Execute_BoundObjectCreation_CreatesConfirmedInvokes()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var ctor = AddMethod(pipeline, ".ctor", "global::Acme.Shared.Contracts.PaymentClient", OrdersProject);
        pipeline.Accumulator.AddObservation(CreateObservation(source.Reference, ObservationKind.ObjectCreation, 1, BoundMessage(ctor)));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(RelationKind.Invokes, relation.Kind);
        Assert.Equal(source.Reference, relation.Source);
        Assert.Equal(ctor.Reference, relation.Target);
    }

    [Fact]
    [Trait("Requirement", "CLLF-04")]
    public void Execute_OwnerIsNotASymbol_DoesNotCreateConfirmedInvokes()
    {
        var pipeline = Arrange();
        var target = AddMethod(pipeline, "Authorize", "global::Acme.Shared.Contracts.PaymentClient", OrdersProject);
        var component = Component.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj", []);
        pipeline.Accumulator.AddFact(component);
        pipeline.Accumulator.AddObservation(CreateObservation(component.Reference, ObservationKind.Invocation, 1, BoundMessage(target)));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        Assert.DoesNotContain(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            relation => relation.Kind is RelationKind.Invokes);
    }

    [Fact]
    [Trait("Requirement", "CLLF-05")]
    public void Execute_DuplicateOccurrencesOfSamePair_EmitsOneConfirmedInvokes()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var target = AddMethod(pipeline, "Authorize", "global::Acme.Shared.Contracts.PaymentClient", OrdersProject);
        AddBoundInvocation(pipeline, source, target, ordinal: 1);
        AddBoundInvocation(pipeline, source, target, ordinal: 2);
        var context = new ClassifierContext(pipeline);

        var result = new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Equal(1, result.RelationCount);
        Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CLLF-07")]
    [Trait("Requirement", "CLLF-09")]
    public void Execute_AbstractTargetWithConcreteImplementors_EmitsCandidatesNotConfirmed()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var abstractTarget = AddMethod(
            pipeline,
            "Authorize",
            "global::Acme.Orders.IPaymentGateway",
            OrdersProject,
            [SymbolFacet.Callable, SymbolFacet.Abstract]);
        var implementor = AddMethod(pipeline, "Authorize", "global::Acme.Orders.PaymentGateway", OrdersProject);
        AddBoundInvocation(pipeline, source, abstractTarget, ordinal: 1);
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        var candidate = Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates.ToArray());
        Assert.Equal(RelationKind.Invokes, candidate.Kind);
        Assert.Equal(source.Reference, candidate.Source);
        Assert.Equal(implementor.Reference, candidate.ProposedTarget);
        Assert.DoesNotContain(
            pipeline.Accumulator.ToSnapshot().ConfirmedRelations,
            relation => relation.Target.Equals(abstractTarget.Reference));
    }

    [Fact]
    [Trait("Requirement", "CLLF-08")]
    public void Execute_AbstractTargetWithoutImplementors_EmitsUnresolvedNoCandidateFound()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var abstractTarget = AddMethod(
            pipeline,
            "Authorize",
            "global::Acme.Orders.IPaymentGateway",
            OrdersProject,
            [SymbolFacet.Callable, SymbolFacet.Abstract]);
        AddBoundInvocation(pipeline, source, abstractTarget, ordinal: 1);
        var context = new ClassifierContext(pipeline);

        var result = new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Equal(1, result.UnresolvedCount);
        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(RelationKind.Invokes, unresolved.Kind);
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        Assert.Equal(source.Reference, unresolved.Source);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CLLF-10")]
    public void Execute_ConcreteDeclaredReceiver_ConfirmsConcreteTargetNotCandidate()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        AddMethod(
            pipeline,
            "Authorize",
            "global::Acme.Orders.IPaymentGateway",
            OrdersProject,
            [SymbolFacet.Callable, SymbolFacet.Abstract]);
        var concrete = AddMethod(pipeline, "Authorize", "global::Acme.Orders.PaymentGateway", OrdersProject);
        AddBoundInvocation(pipeline, source, concrete, ordinal: 1);
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(concrete.Reference, relation.Target);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Candidates);
    }

    [Fact]
    [Trait("Requirement", "CLLF-11")]
    public void Execute_UnboundObservation_EmitsUnresolvedAndOpenFrontier()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        pipeline.Accumulator.AddObservation(
            CreateObservation(
                source.Reference,
                ObservationKind.Invocation,
                1,
                diagnostic: new BindingDiagnostic("unbound", "The occurrence did not bind.")));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        var frontier = Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
        Assert.Equal(FrontierCause.FurtherContinuationObserved, frontier.Cause);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
    }

    [Fact]
    [Trait("Requirement", "CLLF-11")]
    public void Execute_DelegateInvoke_EmitsOpenFrontierWithoutConfirmedInvokes()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var invoke = CanonicalSymbolSignature.Create("method", "global::System.Action", "Invoke", 0, "global::System.Void");
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + invoke.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
        Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CLLF-11")]
    public void Execute_ReflectionDispatch_EmitsOpenFrontier()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var invoke = CanonicalSymbolSignature.Create(
            "method",
            "global::System.Reflection.MethodInfo",
            "Invoke",
            0,
            "global::System.Object",
            [new SymbolParameterSignature("global::System.Object"), new SymbolParameterSignature("global::System.Object[]")]);
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + invoke.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CLLF-12")]
    public void Execute_GenericTypeParameterCallWithNamedCallables_EmitsCandidatesAndFrontier()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "Dispatch", "global::Acme.Orders.Dispatcher", OrdersProject);
        var named = AddMethod(pipeline, "Handle", "global::Acme.Orders.OrderHandler", OrdersProject);
        var typeParameter = CanonicalSymbolSignature.Create("method", "THandler", "Handle", 0, "global::System.Void");
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + typeParameter.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var candidate = Assert.Single(pipeline.Accumulator.ToSnapshot().Candidates.ToArray());
        Assert.Equal(named.Reference, candidate.ProposedTarget);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
    }

    [Fact]
    [Trait("Requirement", "CLLF-12")]
    public void Execute_GenericTypeParameterCallWithoutCandidates_EmitsUnresolvedAndFrontier()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "Dispatch", "global::Acme.Orders.Dispatcher", OrdersProject);
        var typeParameter = CanonicalSymbolSignature.Create("method", "THandler", "Handle", 0, "global::System.Void");
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + typeParameter.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CLLF-13")]
    public void Execute_UnresolvedRecord_AlsoEmitsOpenFrontier()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var external = CanonicalSymbolSignature.Create(
            "method",
            "global::Newtonsoft.Json.JsonConvert",
            "SerializeObject",
            0,
            "global::System.String");
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + external.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
    }

    [Fact]
    [Trait("Requirement", "CLLF-14")]
    public void Execute_FallbackOwner_EmitsInsufficientEvidenceWithoutOpenFrontier()
    {
        var pipeline = Arrange();
        var fallback = pipeline.Accumulator.ToSnapshot().Facts.OfType<Symbol>()
            .Where(symbol => symbol.OwningProject.Equals(OrdersProject))
            .OrderBy(symbol => symbol.Signature.Value, StringComparer.Ordinal)
            .First();
        var target = AddMethod(pipeline, "Authorize", "global::Acme.Shared.Contracts.PaymentClient", OrdersProject);
        AddBoundInvocation(pipeline, fallback, target, ordinal: 1);
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Frontiers);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
    }

    [Fact]
    [Trait("Requirement", "CLLF-15")]
    [Trait("Requirement", "CLLF-16")]
    public void Execute_ReceiverShapes_ProduceOneConfirmedInvokesForTheSamePair()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "ViaShapes", "global::Acme.Orders.ReceiverShapes", OrdersProject);
        var target = AddMethod(pipeline, "Authorize", "global::Acme.Shared.Contracts.PaymentClient", OrdersProject);
        AddBoundInvocation(pipeline, source, target, ordinal: 1);
        AddBoundInvocation(pipeline, source, target, ordinal: 2);
        AddBoundInvocation(pipeline, source, target, ordinal: 3);
        AddBoundInvocation(pipeline, source, target, ordinal: 4);
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(source.Reference, relation.Source);
        Assert.Equal(target.Reference, relation.Target);
    }

    [Fact]
    [Trait("Requirement", "CLLF-17")]
    public void Execute_CrossProjectTargetInSameSolution_CreatesConfirmedInvokes()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "AuthorizeViaPaymentClientAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var target = AddMethod(pipeline, "Authorize", "global::Acme.Shared.Contracts.PaymentClient", SharedProject);
        AddBoundInvocation(pipeline, source, target, ordinal: 1);
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var relation = Assert.Single(pipeline.Accumulator.ToSnapshot().ConfirmedRelations.ToArray());
        Assert.Equal(source.Reference, relation.Source);
        Assert.Equal(target.Reference, relation.Target);
        Assert.NotEqual(source.OwningProject, target.OwningProject);
    }

    [Fact]
    [Trait("Requirement", "CLLF-18")]
    public void Execute_ExternalPackageTarget_EmitsUnresolvedNoCandidateFoundAndFrontier()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var external = CanonicalSymbolSignature.Create(
            "method",
            "global::Acme.Payments.Grpc.PaymentsClient",
            "Authorize",
            0,
            "global::System.Threading.Tasks.Task");
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + external.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        var unresolved = Assert.Single(pipeline.Accumulator.ToSnapshot().Unresolved.ToArray());
        Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause);
        Assert.Single(pipeline.Accumulator.ToSnapshot().Frontiers.ToArray());
        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
    }

    [Fact]
    [Trait("Requirement", "CLLF-20")]
    public void Execute_BclFrameworkTarget_SilentlySkips()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var format = CanonicalSymbolSignature.Create(
            "method",
            "global::System.String",
            "Format",
            0,
            "global::System.String");
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + format.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Frontiers);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "CLLF-20")]
    public void Execute_MicrosoftFrameworkTarget_SilentlySkips()
    {
        var pipeline = Arrange();
        var source = AddMethod(pipeline, "PlaceOrderAsync", "global::Acme.Orders.OrderService", OrdersProject);
        var log = CanonicalSymbolSignature.Create(
            "method",
            "global::Microsoft.Extensions.Logging.LoggerExtensions",
            "LogInformation",
            0,
            "global::System.Void");
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, 1, "bound::" + log.Value));
        var context = new ClassifierContext(pipeline);

        new InvokesPass().Execute(context, CancellationToken.None);

        Assert.Empty(pipeline.Accumulator.ToSnapshot().ConfirmedRelations);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "CLLF-01")]
    public void Identity_IsInvokesClassifierVersionOne()
    {
        Assert.Equal("csharp2md.classifier.invokes", InvokesPass.Identity.Id);
        Assert.Equal(1, InvokesPass.Identity.Version);
        Assert.Equal("Invokes", new InvokesPass().Name);
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.AnalysisVariants = [AnalysisVariantId.Create("net10.0", "Debug", [], "local")];
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(Project.Create(OrdersProject));
        pipeline.Accumulator.AddFact(Project.Create(SharedProject));
        AddMethod(pipeline, "0-fallback", "global::Acme.Orders.0Fallback", OrdersProject);
        AddMethod(pipeline, "0-fallback", "global::Acme.Shared.Contracts.0Fallback", SharedProject);
        return pipeline;
    }

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static ProjectId SharedProject =>
        ProjectId.Create(AcmeSolution, "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");

    private static Symbol AddMethod(
        PipelineContext pipeline,
        string metadata,
        string container,
        ProjectId project,
        IEnumerable<SymbolFacet>? facets = null)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", container, metadata, 0, "global::System.Void"),
            project,
            SymbolFacetSet.Create(facets ?? [SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static void AddBoundInvocation(PipelineContext pipeline, Symbol source, Symbol target, int ordinal) =>
        pipeline.Accumulator.AddObservation(
            CreateObservation(source.Reference, ObservationKind.Invocation, ordinal, BoundMessage(target)));

    private static string BoundMessage(Symbol target) => "bound::" + target.Signature.Value;

    private static Observation CreateObservation(
        FactReference owner,
        ObservationKind kind,
        int ordinal,
        string? boundMessage = null,
        BindingDiagnostic? diagnostic = null) =>
        Observation.Create(
            owner,
            kind,
            NormalizedPayload.Create([]),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Orders/OrderService.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            diagnostic ?? new BindingDiagnostic("bound", boundMessage ?? "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
