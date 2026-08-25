namespace Csharp2Md.Storage.Wire;

public sealed record ComponentDto(
    FactReferenceDto Identity,
    string Solution,
    string Name,
    ImmutableArray<FactReferenceDto> Owners,
    string ContentSha256);

public sealed record DeploymentUnitDto(
    FactReferenceDto Identity,
    string Solution,
    string Name,
    string ContentSha256);

public sealed record EntryPointDto(
    FactReferenceDto Identity,
    FactReferenceDto Symbol,
    FactReferenceDto OwningComponent,
    string ContentSha256);

public sealed record BoundaryOperationDto(
    FactReferenceDto Identity,
    FactReferenceDto Symbol,
    FactReferenceDto OwningComponent,
    string Direction,
    string? Protocol,
    string? DestinationScope,
    string? HttpMethod,
    StructuralLiteralDto? Route,
    StructuralLiteralDto? ProtocolOperationKey,
    string ContentSha256);

public sealed record ExternalSystemDto(
    FactReferenceDto Identity,
    string Solution,
    StructuralLiteralDto Name,
    string ContentSha256);
