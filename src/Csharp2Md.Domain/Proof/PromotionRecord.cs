using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Proof;

public sealed class PromotionRecord
{
    private static readonly ImmutableHashSet<string> KnownFacetWireValues = BuildKnownFacetWireValues();

    public ImmutableArray<ObservationIdentity> RequiredObservations { get; }

    public ImmutableArray<EvidenceMethod> AcceptedEvidenceMethods { get; }

    public ImmutableArray<string> NegativeConditions { get; }

    public ImmutableArray<FactReference> ProducedFacts { get; }

    public ImmutableArray<RelationKind> ProducedRelations { get; }

    public ImmutableArray<string> ProducedFacets { get; }

    public ImmutableArray<RejectedCandidate> RejectedCandidates { get; }

    public ClassifierIdentity Classifier { get; }

    private PromotionRecord(
        ImmutableArray<ObservationIdentity> requiredObservations,
        ImmutableArray<EvidenceMethod> acceptedEvidenceMethods,
        ImmutableArray<string> negativeConditions,
        ImmutableArray<FactReference> producedFacts,
        ImmutableArray<RelationKind> producedRelations,
        ImmutableArray<string> producedFacets,
        ImmutableArray<RejectedCandidate> rejectedCandidates,
        ClassifierIdentity classifier)
    {
        RequiredObservations = requiredObservations;
        AcceptedEvidenceMethods = acceptedEvidenceMethods;
        NegativeConditions = negativeConditions;
        ProducedFacts = producedFacts;
        ProducedRelations = producedRelations;
        ProducedFacets = producedFacets;
        RejectedCandidates = rejectedCandidates;
        Classifier = classifier;
    }

    public static PromotionRecord Create(
        ImmutableArray<ObservationIdentity> requiredObservations,
        ImmutableArray<EvidenceMethod> acceptedEvidenceMethods,
        ImmutableArray<string> negativeConditions,
        ImmutableArray<FactReference> producedFacts,
        ImmutableArray<RelationKind> producedRelations,
        ImmutableArray<string> producedFacets,
        ImmutableArray<RejectedCandidate> rejectedCandidates,
        ClassifierIdentity classifier)
    {
        RequireProvided(requiredObservations, nameof(requiredObservations));
        if (requiredObservations.Length == 0)
        {
            throw new ArgumentException("A promotion record must require at least one observation.", nameof(requiredObservations));
        }

        RequireProvided(acceptedEvidenceMethods, nameof(acceptedEvidenceMethods));
        foreach (var method in acceptedEvidenceMethods)
        {
            if (!Enum.IsDefined(method))
            {
                throw new ArgumentException($"'{method}' is not a defined value of the '{nameof(EvidenceMethod)}' axis.", nameof(acceptedEvidenceMethods));
            }
        }

        RequireProvided(negativeConditions, nameof(negativeConditions));
        foreach (var condition in negativeConditions)
        {
            FactIdGrammar.RequireCanonicalText(condition, nameof(negativeConditions));
        }

        RequireProvided(producedFacts, nameof(producedFacts));

        RequireProvided(producedRelations, nameof(producedRelations));
        foreach (var relation in producedRelations)
        {
            if (!Enum.IsDefined(relation))
            {
                throw new ArgumentException($"'{relation}' is not a defined value of the '{nameof(RelationKind)}' axis.", nameof(producedRelations));
            }
        }

        RequireProvided(producedFacets, nameof(producedFacets));
        foreach (var facet in producedFacets)
        {
            if (!KnownFacetWireValues.Contains(facet))
            {
                throw new ArgumentException($"'{facet}' is not a registered facet axis value.", nameof(producedFacets));
            }
        }

        RequireProvided(rejectedCandidates, nameof(rejectedCandidates));

        if (classifier.Equals(default(ClassifierIdentity)))
        {
            throw new ArgumentException("A promotion record requires a classifier identity.", nameof(classifier));
        }

        return new PromotionRecord(
            requiredObservations,
            acceptedEvidenceMethods,
            negativeConditions,
            producedFacts,
            producedRelations,
            producedFacets,
            rejectedCandidates,
            classifier);
    }

    private static void RequireProvided<T>(ImmutableArray<T> value, string parameterName)
    {
        if (value.IsDefault)
        {
            throw new ArgumentException($"A promotion record requires {parameterName}.", parameterName);
        }
    }

    private static ImmutableHashSet<string> BuildKnownFacetWireValues()
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        AddWireValues<BoundaryProtocol>(builder);
        AddWireValues<BoundaryDirection>(builder);
        AddWireValues<BoundaryRole>(builder);
        AddWireValues<DataStoreTechnology>(builder);
        AddWireValues<DataObjectForm>(builder);
        AddWireValues<DataOperationKind>(builder);
        AddWireValues<SymbolFacet>(builder);
        AddWireValues<MappingStateKind>(builder);
        return builder.ToImmutable();
    }

    private static void AddWireValues<TEnum>(ImmutableHashSet<string>.Builder builder)
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            builder.Add(FacetAxes.WireValue(value));
        }
    }
}
