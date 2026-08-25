using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class ConfirmedRelationCreateCallableTests
{
    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");

    private static FactReference ComponentReference(string name) =>
        new(new FactId("component", $"id1:component;name={name}"), "Component");

    private static FactReference TypedReference(string factType) =>
        new(new FactId(factType.ToLowerInvariant(), $"id1:{factType.ToLowerInvariant()};case=callable"), factType);

    private static Symbol MethodSymbol(string metadataName, params SymbolFacet[] facets)
    {
        var signature = CanonicalSymbolSignature.Create("method", "global::Acme.Payment", metadataName, 0, "global::System.Void");
        return Symbol.Create(signature, AcmeProject, SymbolFacetSet.Create(facets));
    }

    private static FacetBinding EmptyFacets() => FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

    private static EvidenceChain Evidence(FactReference owner) =>
        EvidenceChain.Create([new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), 1)]);

    private static ClassifierIdentity Classifier() => ClassifierIdentity.Create("csharp2md.relations.callable", 1);

    private static ImmutableArray<AnalysisVariantId> Variants() =>
        [AnalysisVariantId.Create("net10.0", "Release", [], "ci")];

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_InvokesWithANonCallableSourceSymbol_IsRejectedNamingTheRelation()
    {
        var source = MethodSymbol("Caller");
        var target = MethodSymbol("Callee", SymbolFacet.Callable);

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Invokes,
            source.Reference,
            target.Reference,
            EmptyFacets(),
            Evidence(source.Reference),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic,
            source,
            target));

        Assert.Equal("sourceFact", exception.ParamName);
        Assert.Contains(nameof(RelationKind.Invokes), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_ExecutesWithANonCallableTargetSymbol_IsRejectedNamingTargetFact()
    {
        var target = MethodSymbol("Main");

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Executes,
            TypedReference("EntryPoint"),
            target.Reference,
            EmptyFacets(),
            Evidence(TypedReference("EntryPoint")),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic,
            targetFact: target));

        Assert.Equal("targetFact", exception.ParamName);
        Assert.Contains(nameof(RelationKind.Executes), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_ExecutesUsesTargetFact_SoANonCallableSourceFactDoesNotRejectACallableTarget()
    {
        var target = MethodSymbol("Main", SymbolFacet.Callable);
        var source = EntryPoint.Create(target.Reference, ComponentReference("payments-api"));

        var relation = ConfirmedRelation.Create(
            RelationKind.Executes,
            source.Reference,
            target.Reference,
            EmptyFacets(),
            Evidence(source.Reference),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic,
            source,
            target);

        Assert.Equal(RelationKind.Executes, relation.Kind);
        Assert.Equal(source.Reference, relation.Source);
        Assert.Equal(target.Reference, relation.Target);
    }

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_InvokesUsesSourceFact_SoANonCallableTargetDoesNotRejectACallableSource()
    {
        var source = MethodSymbol("Caller", SymbolFacet.Callable);
        var target = MethodSymbol("Callee");

        var relation = ConfirmedRelation.Create(
            RelationKind.Invokes,
            source.Reference,
            target.Reference,
            EmptyFacets(),
            Evidence(source.Reference),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic,
            source,
            target);

        Assert.Equal(RelationKind.Invokes, relation.Kind);
        Assert.Equal(source.Reference, relation.Source);
        Assert.Equal(target.Reference, relation.Target);
    }

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_ImplementsOperationWithANonCallableSourceSymbol_IsRejectedNamingSourceFact()
    {
        var source = MethodSymbol("Handle");

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.ImplementsOperation,
            source.Reference,
            TypedReference("BoundaryOperation"),
            EmptyFacets(),
            Evidence(source.Reference),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic,
            source));

        Assert.Equal("sourceFact", exception.ParamName);
        Assert.Contains(nameof(RelationKind.ImplementsOperation), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_AccessesDataWithANonCallableSourceSymbol_IsRejectedNamingSourceFact()
    {
        var source = MethodSymbol("Load");

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.AccessesData,
            source.Reference,
            TypedReference("DataOperation"),
            EmptyFacets(),
            Evidence(source.Reference),
            Classifier(),
            Variants(),
            EvidenceMethod.Semantic,
            source));

        Assert.Equal("sourceFact", exception.ParamName);
        Assert.Contains(nameof(RelationKind.AccessesData), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_SourceFactWhoseReferenceDoesNotEqualSource_IsRejectedNamingSourceFact()
    {
        var mismatched = MethodSymbol("Charge", SymbolFacet.Callable);

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Contains,
            TypedReference("Solution"),
            TypedReference("Project"),
            EmptyFacets(),
            Evidence(TypedReference("Solution")),
            Classifier(),
            Variants(),
            EvidenceMethod.Syntactic,
            mismatched));

        Assert.Equal("sourceFact", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "ENG-55")]
    public void Create_TargetFactWhoseReferenceDoesNotEqualTarget_IsRejectedNamingTargetFact()
    {
        var mismatched = MethodSymbol("Charge", SymbolFacet.Callable);

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Contains,
            TypedReference("Solution"),
            TypedReference("Project"),
            EmptyFacets(),
            Evidence(TypedReference("Solution")),
            Classifier(),
            Variants(),
            EvidenceMethod.Syntactic,
            targetFact: mismatched));

        Assert.Equal("targetFact", exception.ParamName);
    }
}
