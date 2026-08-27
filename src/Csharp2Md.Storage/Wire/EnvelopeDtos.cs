using System.Text.Json;
using System.Text.Json.Serialization;

namespace Csharp2Md.Storage.Wire;

public sealed record ManifestEnvelope(
    int SchemaVersion,
    int TaxonomyVersion,
    int ObservationSchemaVersion,
    string SolutionKey,
    string SolutionFileName,
    ImmutableArray<ManifestEntry> Artifacts);

public sealed record ManifestEntry(
    string CanonicalKey,
    string Role,
    int Count,
    string Path);

public sealed record BatchManifestEnvelope(
    int SchemaVersion,
    bool Complete,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? IncompleteScopeReason,
    ImmutableArray<BatchManifestSolutionEntry> Solutions,
    ImmutableArray<BatchManifestArtifactEntry> Artifacts);

public sealed record BatchManifestSolutionEntry(
    string Identity,
    string SolutionFileName,
    string PackageDirectory,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailingStage);

public sealed record BatchManifestArtifactEntry(
    string CanonicalKey,
    string Role,
    int Count);

public sealed record CoverageMetricDto(
    int Numerator,
    int Denominator,
    int Exclusions,
    int Unknowns,
    ImmutableArray<string> DegradationReasons);

public sealed record CoverageEnvelope(
    CoverageMetricDto EntryPointCoverage,
    CoverageMetricDto LinkedCallCoverage,
    CoverageMetricDto ContractCoverage,
    CoverageMetricDto PersistenceCoverage);

public sealed record RunCertificationEnvelope(string Status);

public sealed record DiagnosticRecordDto(string Code, string Message, string? IdentityOrKey);

public sealed record DiagnosticsEnvelope(ImmutableArray<DiagnosticRecordDto> Records);

public sealed record QuarantineRecordDto(
    string RecordKind,
    string IdentityOrKey,
    string Gate,
    string Detail,
    JsonElement Payload);

public sealed record QuarantineEnvelope(ImmutableArray<QuarantineRecordDto> Records);

public sealed record MeasurementRecordDto(
    string Name,
    string? Timestamp,
    long? DurationMilliseconds);

public sealed record MeasurementsEnvelope(ImmutableArray<MeasurementRecordDto> Records);

public sealed record RedactionEnvelopeDto(
    string Document,
    string Artifact,
    bool Redacted,
    ImmutableArray<SourceSpanDto> RedactedSpans,
    string OriginalSha256,
    string PublishedSha256);
