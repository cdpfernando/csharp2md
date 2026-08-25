using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class ConfirmedRelationCreateTargetShapeTests
{
    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");

    private static FactReference ComponentReference(string name) =>
        new(new FactId("component", $"id1:component;name={name}"), "Component");

    private static FactReference TypedReference(string factType) =>
        new(new FactId(factType.ToLowerInvariant(), $"id1:{factType.ToLowerInvariant()};case=target-shape"), factType);

    private static Symbol MethodSymbol(string metadataName, params SymbolFacet[] facets)
    {
        var signature = CanonicalSymbolSignature.Create("method", "global::Acme.Payment", metadataName, 0, "global::System.Void");
        return Symbol.Create(signature, AcmeProject, SymbolFacetSet.Create(facets));
    }

    private static FacetBinding EmptyFacets() => FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static EvidenceChain Evidence(FactReference owner) =>
        EvidenceChain.Create([new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), 1)]);

    private static ClassifierIdentity Classifier() => ClassifierIdentity.Create("csharp2md.relations.target-shape", 1);

    private static ImmutableArray<AnalysisVariantId> Variants() =>
        [AnalysisVariantId.Create("net10.0", "Release", [], "ci")];

    private static BoundaryOperation OutboundOperation() =>
        BoundaryOperation.Create(
            MethodSymbol("Charge").Reference,
            ComponentReference("payments-api"),
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            "external",
            "GET",
            StructuralLiteral.Create(LiteralRole.Route, "/v1/charges", "route"));

    private static DataStore OrdersStore() =>
        DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders-db", "name"));

    [Fact]
    [Trait("Requirement", "ENG-56")]
    public void Create_TargetsWithAnOutboundBoundaryOperation_IsRejected()
    {
        var outbound = OutboundOperation();

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Targets,
            TypedReference("BoundaryOperation"),
            outbound.Reference,
            EmptyFacets(),
            Evidence(TypedReference("BoundaryOperation")),
            Classifier(),
            Variants(),
            EvidenceMethod.Configured,
            targetFact: outbound));

        Assert.Contains(nameof(RelationKind.Targets), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(BoundaryDirection.Outbound), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-57")]
    public void Create_OperatesOnWithANonDataObjectOrDataFieldTarget_IsRejected()
    {
        var store = OrdersStore();

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.OperatesOn,
            TypedReference("DataOperation"),
            store.Reference,
            EmptyFacets(),
            Evidence(TypedReference("DataOperation")),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic,
            targetFact: store));

        Assert.Contains(nameof(RelationKind.OperatesOn), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(DataStore), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-56")]
    public void Create_TargetsWithoutTargetFact_IsRejectedNamingTargetFact()
    {
        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Targets,
            TypedReference("BoundaryOperation"),
            TypedReference("DeploymentUnit"),
            EmptyFacets(),
            Evidence(TypedReference("BoundaryOperation")),
            Classifier(),
            Variants(),
            EvidenceMethod.Configured));

        Assert.Equal("targetFact", exception.ParamName);
        Assert.Contains(nameof(RelationKind.Targets), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-57")]
    public void Create_OperatesOnWithoutTargetFact_IsRejectedNamingTargetFact()
    {
        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.OperatesOn,
            TypedReference("DataOperation"),
            TypedReference("DataObject"),
            EmptyFacets(),
            Evidence(TypedReference("DataOperation")),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic));

        Assert.Equal("targetFact", exception.ParamName);
        Assert.Contains(nameof(RelationKind.OperatesOn), exception.Message, StringComparison.Ordinal);
    }
}
