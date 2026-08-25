namespace Csharp2Md.Storage.Wire;

public sealed record ObservationDto(
    ObservationIdentityDto Identity,
    EvidenceLocatorDto Locator,
    string EvidenceMethod,
    BindingDiagnosticDto Diagnostic,
    string DocumentHash,
    int ExtractorVersion,
    string ContentSha256);
