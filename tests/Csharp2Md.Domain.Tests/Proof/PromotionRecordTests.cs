using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Proof;

public sealed class PromotionRecordTests
{
    private static readonly ImmutableArray<ObservationIdentity> ValidRequiredObservations =
    [
        new(
            new FactReference(FactIdGrammar.Create("widget", ("name", "value")), "widget"),
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            1),
    ];

    private static readonly ImmutableArray<EvidenceMethod> ValidAcceptedEvidenceMethods = [EvidenceMethod.Semantic];

    private static readonly ImmutableArray<string> ValidNegativeConditions = ["no-conflicting-attribute"];

    private static readonly ImmutableArray<FactReference> ValidProducedFacts =
        [new(FactIdGrammar.Create("widget", ("name", "value")), "widget")];

    private static readonly ImmutableArray<RelationKind> ValidProducedRelations = [RelationKind.Invokes];

    private static readonly ImmutableArray<string> ValidProducedFacets = [FacetAxes.WireValue(SymbolFacet.Callable)];

    private static readonly ImmutableArray<RejectedCandidate> ValidRejectedCandidates = [];

    private static readonly ClassifierIdentity ValidClassifier = ClassifierIdentity.Create("io.csharp2md.classifier", 1);

    private static PromotionRecord Build(
        ImmutableArray<ObservationIdentity> requiredObservations,
        ImmutableArray<EvidenceMethod> acceptedEvidenceMethods,
        ImmutableArray<string> negativeConditions,
        ImmutableArray<FactReference> producedFacts,
        ImmutableArray<RelationKind> producedRelations,
        ImmutableArray<string> producedFacets,
        ImmutableArray<RejectedCandidate> rejectedCandidates,
        ClassifierIdentity classifier) =>
        PromotionRecord.Create(
            requiredObservations,
            acceptedEvidenceMethods,
            negativeConditions,
            producedFacts,
            producedRelations,
            producedFacets,
            rejectedCandidates,
            classifier);

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_AllNineDeclaredComponentsProvided_ProducesTheRecord()
    {
        var record = Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier);

        Assert.Equal(ValidRequiredObservations, record.RequiredObservations);
        Assert.Equal(ValidAcceptedEvidenceMethods, record.AcceptedEvidenceMethods);
        Assert.Equal(ValidNegativeConditions, record.NegativeConditions);
        Assert.Equal(ValidProducedFacts, record.ProducedFacts);
        Assert.Equal(ValidProducedRelations, record.ProducedRelations);
        Assert.Equal(ValidProducedFacets, record.ProducedFacets);
        Assert.Equal(ValidRejectedCandidates, record.RejectedCandidates);
        Assert.Equal(ValidClassifier, record.Classifier);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingRequiredObservations_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            default, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

        Assert.Equal("requiredObservations", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_EmptyRequiredObservations_IsRejected_SoAPromotionWithNoEvidenceRequirementCannotExist() =>
        Assert.Throws<ArgumentException>(() => Build(
            [], ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingAcceptedEvidenceMethods_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, default, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

        Assert.Equal("acceptedEvidenceMethods", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_UndefinedEvidenceMethod_IsRejected() =>
        Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, [(EvidenceMethod)(-1)], ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingNegativeConditions_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, default, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

        Assert.Equal("negativeConditions", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingProducedFacts_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, default,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

        Assert.Equal("producedFacts", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingProducedRelations_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            default, ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

        Assert.Equal("producedRelations", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_UndefinedRelationKind_IsRejected() =>
        Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            [(RelationKind)(-1)], ValidProducedFacets, ValidRejectedCandidates, ValidClassifier));

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingProducedFacets_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, default, ValidRejectedCandidates, ValidClassifier));

        Assert.Equal("producedFacets", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_UnregisteredFacetWireValue_IsRejectedNamingTheOffendingValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ["not-a-registered-facet"], ValidRejectedCandidates, ValidClassifier));

        Assert.Contains("not-a-registered-facet", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingRejectedCandidates_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, default, ValidClassifier));

        Assert.Equal("rejectedCandidates", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-67")]
    public void Create_MissingClassifier_IsRejectedNamingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Build(
            ValidRequiredObservations, ValidAcceptedEvidenceMethods, ValidNegativeConditions, ValidProducedFacts,
            ValidProducedRelations, ValidProducedFacets, ValidRejectedCandidates, default));

        Assert.Equal("classifier", exception.ParamName);
    }
}
