using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests;

public sealed class ClassifierFactMappingTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");

    private static CanonicalSymbolSignature RunSignature => CanonicalSymbolSignature.Create(
        "method", "global::Acme.Payment", "Run", 0, "global::System.Void");

    public static TheoryData<string, FactualSnapshot> ClassifierFactFixtures() => new()
    {
        { "Component", Snapshot(Component.Create(AcmeSolution, "Payments.Api", [CallableSymbol().Reference])) },
        { "EntryPoint", Snapshot(EntryPoint.Create(CallableSymbol().Reference, ComponentRef())) },
        { "BoundaryOperation", Snapshot(InboundOperation()) },
        { "ExternalSystem", Snapshot(ExternalSystem.Create(
            AcmeSolution, StructuralLiteral.Create(LiteralRole.ClientName, "PaymentService", "name"))) },
        { "Contract", Snapshot(OrderPlacedContract()) },
        { "ContractBinding", Snapshot(ContractBinding.Create(
            InboundOperation().Reference, "request", CallableSymbol().Reference, OrderPlacedContract().Reference)) },
        { "ContractRevision", Snapshot(ContractRevision.Create(OrderPlacedContract().Reference, "fp-1")) },
    };

    [Theory]
    [Trait("Requirement", "EBC-37")]
    [MemberData(nameof(ClassifierFactFixtures))]
    public void RoundTrip_ClassifierFactType_EqualsOriginalUnderDomainEquality(string factType, FactualSnapshot snapshot)
    {
        Assert.Equal(factType, snapshot.Facts[0].Reference.FactType);

        var restored = DomainMapper.FromWire(DomainMapper.ToWire(snapshot, Context));

        Assert.Equal(snapshot.Facts.Length, restored.Facts.Length);
        Assert.Equal(snapshot.Facts[0].Reference.FactType, restored.Facts[0].Reference.FactType);
        Assert.Equal(snapshot.Facts[0], restored.Facts[0]);
    }

    [Fact]
    [Trait("Requirement", "EBC-37")]
    public void RoundTrip_CandidateLinkTargets_EqualsOriginal()
    {
        var operation = InboundOperation();
        var external = ExternalSystem.Create(
            AcmeSolution, StructuralLiteral.Create(LiteralRole.ClientName, "PaymentService", "name"));
        var link = CandidateLink.Create(
            RelationKind.Targets,
            operation.Reference,
            external.Reference,
            ValidEvidence());

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([operation, external], [], [], [link], [], []), Context));

        Assert.Equal(link, Assert.Single(restored.Candidates.ToArray()));
        Assert.Equal(RelationKind.Targets, restored.Candidates[0].Kind);
        Assert.Equal(operation.Reference, restored.Candidates[0].Source);
        Assert.Equal(external.Reference, restored.Candidates[0].ProposedTarget);
    }

    [Fact]
    [Trait("Requirement", "EBC-37")]
    public void RoundTrip_UnresolvedRecordUsesContract_EqualsOriginal()
    {
        var record = UnresolvedRecord.Create(
            RelationKind.UsesContract,
            CallableSymbol().Reference,
            UnresolvedCause.NoCandidateFound,
            ValidEvidence());

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([], [], [], [], [record], []), Context));

        Assert.Equal(record, Assert.Single(restored.Unresolved.ToArray()));
        Assert.Equal(RelationKind.UsesContract, restored.Unresolved[0].Kind);
        Assert.Equal(UnresolvedCause.NoCandidateFound, restored.Unresolved[0].Cause);
    }

    [Fact]
    [Trait("Requirement", "EBC-37")]
    public void RoundTrip_ImplementsOperation_ReconstitutesWithMaterializedCallableSymbol()
    {
        var callable = CallableSymbol();
        var operation = InboundOperation();
        var relation = ConfirmedRelation.Create(
            RelationKind.ImplementsOperation,
            callable.Reference,
            operation.Reference,
            EmptyFacets(),
            ValidEvidence(),
            ClassifierIdentity.Create("csharp2md.classifier.http-inbound", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Semantic,
            sourceFact: callable,
            targetFact: operation);

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([callable, operation], [], [relation], [], [], []), Context));

        Assert.Equal(relation, Assert.Single(restored.ConfirmedRelations.ToArray()));
        Assert.Equal(RelationKind.ImplementsOperation, restored.ConfirmedRelations[0].Kind);
        Assert.Contains(restored.Facts, fact => fact.Equals(callable));
        Assert.Contains(restored.Facts, fact => fact.Equals(operation));
    }

    [Fact]
    [Trait("Requirement", "EBC-37")]
    public void RoundTrip_UsesContract_ReconstitutesWithPayloadRoleFacet()
    {
        var operation = InboundOperation();
        var contract = OrderPlacedContract();
        var relation = ConfirmedRelation.Create(
            RelationKind.UsesContract,
            operation.Reference,
            contract.Reference,
            PayloadRoleFacets("request"),
            ValidEvidence(),
            ClassifierIdentity.Create("csharp2md.classifier.contract-messaging", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Semantic);

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([operation, contract], [], [relation], [], [], []), Context));

        var actual = Assert.Single(restored.ConfirmedRelations.ToArray());
        Assert.Equal(relation, actual);
        Assert.Equal(RelationKind.UsesContract, actual.Kind);
        Assert.Contains(
            actual.Facets.Entries,
            entry => entry.AxisName == "payload-role" && entry.WireValue == "request");
        Assert.Contains(restored.Facts, fact => fact.Equals(contract));
    }

    private static FactualSnapshot Snapshot(IFact fact) =>
        new([fact], [], [], [], [], []);

    private static Symbol CallableSymbol() =>
        Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]));

    private static FactReference ComponentRef() =>
        Component.Create(AcmeSolution, "Payments.Api", []).Reference;

    private static BoundaryOperation InboundOperation() =>
        BoundaryOperation.Create(
            CallableSymbol().Reference,
            ComponentRef(),
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"));

    private static Contract OrderPlacedContract() =>
        Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof"));

    private static FacetBinding EmptyFacets() =>
        FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static FacetBinding PayloadRoleFacets(string role) =>
        FacetBinding.Create(
            TaxonomyTables.Default.FacetAxes.Add(new FacetAxisDescriptor("payload-role", TaxonomyTables.Default.PayloadRoles)),
            ["payload-role"],
            [new FacetBindingEntry("payload-role", role)]);

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create(
        [
            new ObservationIdentity(
                Solution.Create(AcmeSolution).Reference,
                ObservationKind.Invocation,
                NormalizedPayload.Create([]),
                1),
        ]);
}
