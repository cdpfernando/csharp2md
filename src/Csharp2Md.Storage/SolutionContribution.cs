namespace Csharp2Md.Storage;

public sealed record SolutionContribution(
    string SolutionIdentity,
    string SolutionFileName,
    string PackageDirectory,
    ImmutableArray<ContributedBoundaryOperation> BoundaryOperations,
    ImmutableArray<ContributedIdentity> Contracts,
    ImmutableArray<ContributedNamedIdentity> Components,
    ImmutableArray<ContributedNamedIdentity> DeploymentUnits,
    ImmutableArray<ContributedNamedIdentity> ExternalSystems);

public sealed record ContributedBoundaryOperation(
    string FactId,
    string FactType,
    string Direction,
    string? Protocol,
    string? DestinationScope,
    string? HttpMethod,
    string? Route,
    string? ProtocolOperationKey,
    string ArtifactKey,
    int Ordinal);

public sealed record ContributedIdentity(
    string FactId,
    string FactType,
    string ArtifactKey,
    int Ordinal);

public sealed record ContributedNamedIdentity(
    string FactId,
    string FactType,
    string Name,
    string ArtifactKey,
    int Ordinal);
