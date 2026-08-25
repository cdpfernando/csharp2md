namespace Csharp2Md.Storage.Wire;

public sealed record ConfirmedRelationDto(
    string Kind,
    FactReferenceDto Source,
    FactReferenceDto Target,
    ImmutableArray<FacetBindingEntryDto> Facets,
    ImmutableArray<ObservationIdentityDto> DerivedFrom,
    ClassifierIdentityDto Classifier,
    ImmutableArray<string> AnalysisVariants,
    string EvidenceMethod,
    string ContentSha256);

public sealed record CandidateLinkDto(
    string Kind,
    FactReferenceDto Source,
    FactReferenceDto ProposedTarget,
    ImmutableArray<ObservationIdentityDto> DerivedFrom,
    string ContentSha256);

public sealed record UnresolvedRecordDto(
    string Kind,
    FactReferenceDto Source,
    string Cause,
    ImmutableArray<ObservationIdentityDto> Available,
    string ContentSha256);

public sealed record OpenFrontierDto(
    ObservationIdentityDto Occurrence,
    string Cause,
    string ContentSha256);
