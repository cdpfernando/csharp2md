namespace Csharp2Md.Storage.Wire;

public sealed record FactReferenceDto(string Id, string FactType);

public sealed record StructuralLiteralDto(string Value, string Role);

public sealed record PayloadEntryDto(string Key, StructuralLiteralDto Value);

public sealed record ObservationIdentityDto(
    FactReferenceDto Owner,
    string Kind,
    ImmutableArray<PayloadEntryDto> Payload,
    int OccurrenceOrdinal);

public sealed record SourceSpanDto(int StartLine, int StartColumn, int EndLine, int EndColumn);

public sealed record EvidenceLocatorDto(string Document, string RelativePath, SourceSpanDto Span);

public sealed record BindingDiagnosticDto(string Code, string Message);
