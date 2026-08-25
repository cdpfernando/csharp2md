using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Relations;

public sealed record ConfirmedRelation
{
    private static readonly TaxonomyRegistry Registry = new(TaxonomyTables.Default);

    public RelationKind Kind { get; }

    public FactReference Source { get; }

    public FactReference Target { get; }

    public FacetBinding Facets { get; }

    public EvidenceChain DerivedFrom { get; }

    public ClassifierIdentity Classifier { get; }

    public ImmutableArray<AnalysisVariantId> AnalysisVariants { get; }

    public Resolution Resolution => Resolution.Confirmed;

    private ConfirmedRelation(
        RelationKind kind,
        FactReference source,
        FactReference target,
        FacetBinding facets,
        EvidenceChain derivedFrom,
        ClassifierIdentity classifier,
        ImmutableArray<AnalysisVariantId> analysisVariants)
    {
        Kind = kind;
        Source = source;
        Target = target;
        Facets = facets;
        DerivedFrom = derivedFrom;
        Classifier = classifier;
        AnalysisVariants = analysisVariants;
    }

    public static ConfirmedRelation Create(
        RelationKind kind,
        FactReference source,
        FactReference target,
        FacetBinding facets,
        EvidenceChain derivedFrom,
        ClassifierIdentity classifier,
        ImmutableArray<AnalysisVariantId> analysisVariants,
        EvidenceMethod evidenceMethod)
    {
        FactGuards.RequireDefined(kind, nameof(kind));
        FactGuards.RequireDefined(evidenceMethod, nameof(evidenceMethod));
        FactGuards.RequireInitialized(source, nameof(source));
        FactGuards.RequireInitialized(target, nameof(target));

        if (facets.Entries.IsDefault)
        {
            throw new ArgumentException("A confirmed relation requires initialized facets.", nameof(facets));
        }

        if (derivedFrom.DerivedFrom.IsDefault)
        {
            throw new ArgumentException("A confirmed relation requires an initialized evidence chain.", nameof(derivedFrom));
        }

        if (classifier.Equals(default(ClassifierIdentity)))
        {
            throw new ArgumentException("A confirmed relation requires a classifier identity.", nameof(classifier));
        }

        if (analysisVariants.IsDefault || analysisVariants.IsEmpty)
        {
            throw new ArgumentException("A confirmed relation requires at least one analysis variant.", nameof(analysisVariants));
        }

        Registry.RequireRegisteredTriple(kind, source.FactType, target.FactType);
        RelationShapeGuards.RequireSufficientEvidence(Registry, kind, evidenceMethod);
        RelationShapeGuards.RequirePayloadRoleForUsesContract(kind, facets);

        return new ConfirmedRelation(kind, source, target, facets, derivedFrom, classifier, analysisVariants);
    }

    public bool Equals(ConfirmedRelation? other) =>
        other is not null
        && Kind == other.Kind
        && Source.Equals(other.Source)
        && Target.Equals(other.Target)
        && Facets.Equals(other.Facets)
        && DerivedFrom.Equals(other.DerivedFrom)
        && Classifier.Equals(other.Classifier)
        && AnalysisVariants.AsSpan().SequenceEqual(other.AnalysisVariants.AsSpan());

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Source);
        hash.Add(Target);
        hash.Add(Facets);
        hash.Add(DerivedFrom);
        hash.Add(Classifier);
        foreach (var variant in AnalysisVariants)
        {
            hash.Add(variant);
        }

        return hash.ToHashCode();
    }
}
