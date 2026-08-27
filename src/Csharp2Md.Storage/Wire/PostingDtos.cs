namespace Csharp2Md.Storage.Wire;

public sealed record PostingEntryDto(string ArtifactKey, int Ordinal);

public sealed record PostingGroupDto(string FactId, ImmutableArray<PostingEntryDto> Entries);
