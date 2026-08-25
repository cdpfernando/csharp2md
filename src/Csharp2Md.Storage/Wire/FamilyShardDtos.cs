namespace Csharp2Md.Storage.Wire;

public sealed record StructuralFactsShard(
    ImmutableArray<SolutionDto> Solutions,
    ImmutableArray<ProjectDto> Projects,
    ImmutableArray<DocumentDto> Documents,
    ImmutableArray<SymbolDto> Symbols);

public sealed record ArchitectureFactsShard(
    ImmutableArray<ComponentDto> Components,
    ImmutableArray<DeploymentUnitDto> DeploymentUnits,
    ImmutableArray<EntryPointDto> EntryPoints,
    ImmutableArray<BoundaryOperationDto> BoundaryOperations,
    ImmutableArray<ExternalSystemDto> ExternalSystems);

public sealed record ContractFactsShard(
    ImmutableArray<ContractDto> Contracts,
    ImmutableArray<ContractBindingDto> ContractBindings,
    ImmutableArray<ContractRevisionDto> ContractRevisions);

public sealed record PersistenceFactsShard(
    ImmutableArray<DataStoreDto> DataStores,
    ImmutableArray<DataObjectDto> DataObjects,
    ImmutableArray<DataFieldDto> DataFields,
    ImmutableArray<DataOperationDto> DataOperations);

public sealed record ConfigurationFactsShard(ImmutableArray<ConfigurationBindingDto> ConfigurationBindings);
