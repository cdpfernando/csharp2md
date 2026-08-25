namespace Csharp2Md.Storage.Wire;

public sealed record ContractDto(
    FactReferenceDto Identity,
    StructuralLiteralDto Proof,
    string ContentSha256);

public sealed record ContractBindingDto(
    FactReferenceDto Identity,
    FactReferenceDto Operation,
    string PayloadRole,
    FactReferenceDto ClrSymbol,
    FactReferenceDto Contract,
    string ContentSha256);

public sealed record ContractRevisionDto(
    FactReferenceDto Identity,
    FactReferenceDto Contract,
    string StructuralFingerprint,
    string ContentSha256);

public sealed record DataStoreDto(
    FactReferenceDto Identity,
    string Technology,
    StructuralLiteralDto Name,
    string ContentSha256);

public sealed record DataObjectDto(
    FactReferenceDto Identity,
    FactReferenceDto Store,
    string Form,
    StructuralLiteralDto SchemaName,
    StructuralLiteralDto TableName,
    string MappingState,
    string ContentSha256);

public sealed record DataFieldDto(
    FactReferenceDto Identity,
    FactReferenceDto DataObject,
    StructuralLiteralDto FieldName,
    string MappingState,
    string ContentSha256);

public sealed record DataOperationDto(
    FactReferenceDto Identity,
    FactReferenceDto Target,
    string Operation,
    string MappingState,
    string ContentSha256);

public sealed record ConfigurationBindingDto(
    FactReferenceDto Identity,
    FactReferenceDto BoundFact,
    StructuralLiteralDto ConfigurationKey,
    string ContentSha256);
