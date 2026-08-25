using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

public sealed record WireDocument(
    ManifestEnvelope Manifest,
    ImmutableArray<byte> TaxonomyRegistryCopy,
    ImmutableArray<SolutionDto> Solutions,
    ImmutableArray<ProjectDto> Projects,
    ImmutableArray<DocumentDto> Documents,
    ImmutableArray<SymbolDto> Symbols,
    ImmutableArray<ComponentDto> Components,
    ImmutableArray<DeploymentUnitDto> DeploymentUnits,
    ImmutableArray<EntryPointDto> EntryPoints,
    ImmutableArray<BoundaryOperationDto> BoundaryOperations,
    ImmutableArray<ExternalSystemDto> ExternalSystems,
    ImmutableArray<ContractDto> Contracts,
    ImmutableArray<ContractBindingDto> ContractBindings,
    ImmutableArray<ContractRevisionDto> ContractRevisions,
    ImmutableArray<DataStoreDto> DataStores,
    ImmutableArray<DataObjectDto> DataObjects,
    ImmutableArray<DataFieldDto> DataFields,
    ImmutableArray<DataOperationDto> DataOperations,
    ImmutableArray<ConfigurationBindingDto> ConfigurationBindings,
    ImmutableDictionary<string, ImmutableArray<ObservationDto>> Observations,
    ImmutableDictionary<string, ImmutableArray<ConfirmedRelationDto>> ConfirmedRelations,
    ImmutableArray<CandidateLinkDto> Candidates,
    ImmutableArray<UnresolvedRecordDto> Unresolved,
    ImmutableArray<OpenFrontierDto> Frontiers,
    ImmutableArray<QuarantineRecordDto> Quarantine,
    CoverageEnvelope Coverage,
    RunCertificationEnvelope RunCertification,
    DiagnosticsEnvelope Diagnostics,
    MeasurementsEnvelope Measurements);
