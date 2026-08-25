namespace Csharp2Md.Storage.Wire;

public sealed record SolutionDto(
    FactReferenceDto Identity,
    string SolutionId,
    string ContentSha256);

public sealed record ProjectDto(
    FactReferenceDto Identity,
    string ProjectId,
    string ContentSha256);

public sealed record DocumentDto(
    FactReferenceDto Identity,
    string OwningProject,
    string RelativePath,
    string ContentSha256);

public sealed record SymbolDto(
    FactReferenceDto Identity,
    string OwningProject,
    string CanonicalSymbolSignature,
    ImmutableArray<string> Facets,
    string ContentSha256);
