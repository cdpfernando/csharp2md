using Csharp2Md.Domain.Facts;
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

        return PayloadHash.Attach(
            new ConfirmedRelationDto(
                WireName(relation.Kind),
                WireFactMapping.ToDto(relation.Source),
                WireFactMapping.ToDto(relation.Target),
                [.. relation.Facets.Entries.Select(entry => new FacetBindingEntryDto(entry.AxisName, entry.WireValue))],
                [.. relation.DerivedFrom.DerivedFrom.Select(WireObservationMapping.ToDto)],
                new ProofAgentIdentityDto(relation.Classifier.Id, relation.Classifier.Version),
                [.. relation.AnalysisVariants.Select(variant => variant.Value)],
                minimumEvidence.ToString(),
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });
    }

    public static ConfirmedRelation FromDto(ConfirmedRelationDto dto) =>
        FromDto(dto, factsById: null);

    public static ConfirmedRelation FromDto(ConfirmedRelationDto dto, IReadOnlyDictionary<string, IFact>? factsById)
    {
        var kind = KindFromWire(dto.Kind);
        IFact? sourceFact = null;
        IFact? targetFact = null;
        factsById?.TryGetValue(dto.Source.Id, out sourceFact);
        factsById?.TryGetValue(dto.Target.Id, out targetFact);
        return ConfirmedRelation.Create(
            kind,
            WireFactMapping.FromDto(dto.Source),
            WireFactMapping.FromDto(dto.Target),
            FacetsFromDto(dto.Facets),
            EvidenceChain.Create(dto.DerivedFrom.Select(WireObservationMapping.FromDto)),
            ClassifierIdentity.Create(dto.Classifier.Id, dto.Classifier.Version),
            [.. dto.AnalysisVariants.Select(AnalysisVariantFromValue)],
            Enum.Parse<EvidenceMethod>(dto.EvidenceMethod),
            sourceFact,
            targetFact);
    }

    public static CandidateLinkDto ToDto(CandidateLink relation) =>
        PayloadHash.Attach(
            new CandidateLinkDto(
                WireName(relation.Kind),
                WireFactMapping.ToDto(relation.Source),
                WireFactMapping.ToDto(relation.ProposedTarget),
                [.. relation.DerivedFrom.DerivedFrom.Select(WireObservationMapping.ToDto)],
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static CandidateLink FromDto(CandidateLinkDto dto) =>
        CandidateLink.Create(
            KindFromWire(dto.Kind),
            WireFactMapping.FromDto(dto.Source),
            WireFactMapping.FromDto(dto.ProposedTarget),
            EvidenceChain.Create(dto.DerivedFrom.Select(WireObservationMapping.FromDto)));

    public static UnresolvedRecordDto ToDto(UnresolvedRecord record) =>
        PayloadHash.Attach(
            new UnresolvedRecordDto(
                WireName(record.Kind),
                WireFactMapping.ToDto(record.Source),
                record.Cause.ToString(),
                [.. record.Available.DerivedFrom.Select(WireObservationMapping.ToDto)],
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

    public static UnresolvedRecord FromDto(UnresolvedRecordDto dto) =>
        UnresolvedRecord.Create(
            KindFromWire(dto.Kind),
            WireFactMapping.FromDto(dto.Source),
            Enum.Parse<UnresolvedCause>(dto.Cause),
            EvidenceChain.Create(dto.Available.Select(WireObservationMapping.FromDto)));

    public static OpenFrontierDto ToDto(OpenFrontier frontier) =>
        PayloadHash.Attach(
            new OpenFrontierDto(
                WireObservationMapping.ToDto(frontier.Occurrence),
                frontier.Cause.ToString(),
                string.Empty),
            static (dto, hash) => dto with { ContentSha256 = hash });

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
        var axes = TaxonomyTables.Default.FacetAxes;
        if (allowed.Contains("payload-role", StringComparer.Ordinal))
        {
            axes = axes.Add(new FacetAxisDescriptor("payload-role", TaxonomyTables.Default.PayloadRoles));
        }

        if (allowed.Contains("mapping-role", StringComparer.Ordinal))
        {
            axes = axes.Add(new FacetAxisDescriptor("mapping-role", TaxonomyTables.Default.MappingRoles));
        }

        return FacetBinding.Create(axes, allowed, values);
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
