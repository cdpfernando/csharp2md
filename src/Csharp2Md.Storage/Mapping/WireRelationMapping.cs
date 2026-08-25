using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class WireRelationMapping
{
    public static string WireName(RelationKind kind) =>
        TaxonomyTables.Default.Relations.Single(descriptor => descriptor.Kind == kind).WireName;

    public static RelationKind KindFromWire(string wireName) =>
        TaxonomyTables.Default.Relations.Single(descriptor => descriptor.WireName == wireName).Kind;

    public static ConfirmedRelationDto ToDto(ConfirmedRelation relation)
    {
        var minimumEvidence = TaxonomyTables.Default.Relations
            .Single(descriptor => descriptor.Kind == relation.Kind)
            .MinimumEvidenceMethod;

        return new ConfirmedRelationDto(
            WireName(relation.Kind),
            WireFactMapping.ToDto(relation.Source),
            WireFactMapping.ToDto(relation.Target),
            [.. relation.Facets.Entries.Select(entry => new FacetBindingEntryDto(entry.AxisName, entry.WireValue))],
            [.. relation.DerivedFrom.DerivedFrom.Select(WireObservationMapping.ToDto)],
            new ProofAgentIdentityDto(relation.Classifier.Id, relation.Classifier.Version),
            [.. relation.AnalysisVariants.Select(variant => variant.Value)],
            minimumEvidence.ToString(),
            string.Empty);
    }

    public static ConfirmedRelation FromDto(ConfirmedRelationDto dto)
    {
        var kind = KindFromWire(dto.Kind);
        return ConfirmedRelation.Create(
            kind,
            WireFactMapping.FromDto(dto.Source),
            WireFactMapping.FromDto(dto.Target),
            FacetsFromDto(dto.Facets),
            EvidenceChain.Create(dto.DerivedFrom.Select(WireObservationMapping.FromDto)),
            ClassifierIdentity.Create(dto.Classifier.Id, dto.Classifier.Version),
            [.. dto.AnalysisVariants.Select(AnalysisVariantFromValue)],
            Enum.Parse<EvidenceMethod>(dto.EvidenceMethod));
    }

    public static CandidateLinkDto ToDto(CandidateLink relation) =>
        new(
            WireName(relation.Kind),
            WireFactMapping.ToDto(relation.Source),
            WireFactMapping.ToDto(relation.ProposedTarget),
            [.. relation.DerivedFrom.DerivedFrom.Select(WireObservationMapping.ToDto)],
            string.Empty);

    public static CandidateLink FromDto(CandidateLinkDto dto) =>
        CandidateLink.Create(
            KindFromWire(dto.Kind),
            WireFactMapping.FromDto(dto.Source),
            WireFactMapping.FromDto(dto.ProposedTarget),
            EvidenceChain.Create(dto.DerivedFrom.Select(WireObservationMapping.FromDto)));

    public static UnresolvedRecordDto ToDto(UnresolvedRecord record) =>
        new(
            WireName(record.Kind),
            WireFactMapping.ToDto(record.Source),
            record.Cause.ToString(),
            [.. record.Available.DerivedFrom.Select(WireObservationMapping.ToDto)],
            string.Empty);

    public static UnresolvedRecord FromDto(UnresolvedRecordDto dto) =>
        UnresolvedRecord.Create(
            KindFromWire(dto.Kind),
            WireFactMapping.FromDto(dto.Source),
            Enum.Parse<UnresolvedCause>(dto.Cause),
            EvidenceChain.Create(dto.Available.Select(WireObservationMapping.FromDto)));

    public static OpenFrontierDto ToDto(OpenFrontier frontier) =>
        new(
            WireObservationMapping.ToDto(frontier.Occurrence),
            frontier.Cause.ToString(),
            string.Empty);

    public static OpenFrontier FromDto(OpenFrontierDto dto) =>
        OpenFrontier.Create(
            WireObservationMapping.FromDto(dto.Occurrence),
            Enum.Parse<FrontierCause>(dto.Cause));

    private static FacetBinding FacetsFromDto(ImmutableArray<FacetBindingEntryDto> entries)
    {
        var values = entries.Select(entry => new FacetBindingEntry(entry.AxisName, entry.WireValue)).ToArray();
        var allowed = values
            .Select(entry => entry.AxisName)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();

        return FacetBinding.Create(TaxonomyTables.Default.FacetAxes, allowed, values);
    }

    private static AnalysisVariantId AnalysisVariantFromValue(string value)
    {
        var components = FactIdComponents.Parse(value);
        IEnumerable<string> symbols = [];
        if (components.TryGetValue("symbols", out var symbolText) && symbolText.Length > 0)
        {
            symbols = symbolText.Split(',');
        }

        return AnalysisVariantId.Create(
            components["tfm"],
            components["configuration"],
            symbols,
            components["environment"]);
    }
}
