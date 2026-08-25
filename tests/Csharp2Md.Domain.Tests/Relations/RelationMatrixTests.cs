using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class RelationMatrixTests
{
    public static IEnumerable<object[]> AllRelationKinds() =>
        Enum.GetValues<RelationKind>().Select(kind => new object[] { kind });

    private static FactReference Reference(string factType) =>
        new(new FactId(factType.ToLowerInvariant(), $"id1:{factType.ToLowerInvariant()};case=matrix"), factType);

    private static EvidenceChain Evidence(FactReference owner) =>
        EvidenceChain.Create([new ObservationIdentity(owner, ObservationKind.Invocation, NormalizedPayload.Create([]), 1)]);

    private static ClassifierIdentity Classifier() => ClassifierIdentity.Create("csharp2md.relations.matrix", 1);

    private static ImmutableArray<AnalysisVariantId> Variants() => [AnalysisVariantId.Create("net10.0", "Release", [], "ci")];

    private static FacetBinding FacetsFor(RelationKind kind)
    {
        if (kind != RelationKind.UsesContract)
        {
            return FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);
        }

        var registeredAxes = TaxonomyTables.Default.FacetAxes.Add(new FacetAxisDescriptor("payload-role", PayloadRoleTable.All));
        return FacetBinding.Create(registeredAxes, ["payload-role"], [new FacetBindingEntry("payload-role", PayloadRoleTable.All[0])]);
    }

    private static RelationTriple RegisteredTripleFor(RelationKind kind) =>
        TaxonomyTables.Default.Relations.Single(descriptor => descriptor.Kind == kind).Triples[0];

    [Theory]
    [MemberData(nameof(AllRelationKinds))]
    [Trait("Requirement", "TAX-42")]
    [Trait("Requirement", "TAX-44")]
    public void ConfirmedRelation_Create_OneRegisteredTriplePerRelation_IsAccepted(RelationKind kind)
    {
        var triple = RegisteredTripleFor(kind);
        var source = Reference(triple.SourceFactType);
        var target = Reference(triple.TargetFactType);

        var relation = ConfirmedRelation.Create(
            kind, source, target, FacetsFor(kind), Evidence(source), Classifier(), Variants());

        Assert.Equal(kind, relation.Kind);
        Assert.Equal(source, relation.Source);
        Assert.Equal(target, relation.Target);
    }

    [Theory]
    [MemberData(nameof(AllRelationKinds))]
    [Trait("Requirement", "TAX-42")]
    [Trait("Requirement", "TAX-45")]
    public void ConfirmedRelation_Create_OneUnregisteredTriplePerRelation_IsRejectedNamingSourceRelationAndTarget(RelationKind kind)
    {
        var triple = RegisteredTripleFor(kind);
        var source = Reference(triple.SourceFactType);
        var target = Reference("Solution");

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            kind, source, target, FacetsFor(kind), Evidence(source), Classifier(), Variants()));

        Assert.Contains(triple.SourceFactType, exception.Message, StringComparison.Ordinal);
        Assert.Contains(kind.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Solution", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-44")]
    public void Contains_IsRestrictedToStructuralOwnerAndStructuralChild_RejectedForANonStructuralPair()
    {
        var source = Reference("Solution");
        var target = Reference("Component");

        var exception = Assert.Throws<ArgumentException>(() => ConfirmedRelation.Create(
            RelationKind.Contains, source, target, FacetsFor(RelationKind.Contains), Evidence(source), Classifier(), Variants()));

        Assert.Contains("Solution", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RelationKind.Contains), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Component", exception.Message, StringComparison.Ordinal);
    }
}
