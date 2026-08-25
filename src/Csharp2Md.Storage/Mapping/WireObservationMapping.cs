using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class WireObservationMapping
{
    public static string WireName(ObservationKind kind) =>
        TaxonomyTables.Default.ObservationKinds.Single(descriptor => descriptor.Kind == kind).WireName;

    public static ObservationKind KindFromWire(string wireName) =>
        TaxonomyTables.Default.ObservationKinds.Single(descriptor => descriptor.WireName == wireName).Kind;

    public static ObservationDto ToDto(Observation observation) =>
        new(
            ToDto(observation.Identity),
            ToDto(observation.Locator),
            observation.ExtractionMethod.ToString(),
            new BindingDiagnosticDto(observation.Diagnostic.Code, observation.Diagnostic.Message),
            observation.DocumentHash.Value,
            observation.ExtractorVersion.Value,
            string.Empty);

    public static Observation FromDto(ObservationDto dto) =>
        Observation.Create(
            WireFactMapping.FromDto(dto.Identity.Owner),
            KindFromWire(dto.Identity.Kind),
            NormalizedPayload.Create(dto.Identity.Payload.Select(entry =>
                new PayloadEntry(entry.Key, WireFactMapping.FromLiteral(entry.Value, entry.Key)))),
            dto.Identity.OccurrenceOrdinal,
            FromDto(dto.Locator),
            Enum.Parse<EvidenceMethod>(dto.EvidenceMethod),
            new BindingDiagnostic(dto.Diagnostic.Code, dto.Diagnostic.Message),
            DocumentHash.Create(dto.DocumentHash),
            new ExtractorVersion(dto.ExtractorVersion));

    public static ObservationIdentityDto ToDto(ObservationIdentity identity) =>
        new(
            WireFactMapping.ToDto(identity.Owner),
            WireName(identity.Kind),
            [.. identity.Payload.Entries.Select(entry => new PayloadEntryDto(entry.Key, WireFactMapping.ToDto(entry.Value)))],
            identity.OccurrenceOrdinal);

    public static ObservationIdentity FromDto(ObservationIdentityDto dto) =>
        new(
            WireFactMapping.FromDto(dto.Owner),
            KindFromWire(dto.Kind),
            NormalizedPayload.Create(dto.Payload.Select(entry =>
                new PayloadEntry(entry.Key, WireFactMapping.FromLiteral(entry.Value, entry.Key)))),
            dto.OccurrenceOrdinal);

    private static EvidenceLocatorDto ToDto(EvidenceLocator locator) =>
        new(
            locator.Document.Value,
            locator.RelativePath,
            new SourceSpanDto(locator.Span.StartLine, locator.Span.StartColumn, locator.Span.EndLine, locator.Span.EndColumn));

    private static EvidenceLocator FromDto(EvidenceLocatorDto dto) =>
        new(
            DocumentId.Create(dto.Document),
            dto.RelativePath,
            new SourceSpan(dto.Span.StartLine, dto.Span.StartColumn, dto.Span.EndLine, dto.Span.EndColumn));
}
