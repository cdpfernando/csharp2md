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
    long ByteSize,
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

/// <summary>
/// One degradation reason recorded against a coverage metric, naming how many denominator members it
/// accounts for (GCPC-004).
/// </summary>
public sealed record DegradationReasonDto
{
    public string Code { get; }

    public string Detail { get; }

    public int AffectedCount { get; }

    public DegradationReasonDto(string code, string detail, int affectedCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentOutOfRangeException.ThrowIfNegative(affectedCount);
        Code = code;
        Detail = detail;
        AffectedCount = affectedCount;
    }
}

/// <summary>
/// A coverage metric's numerator, denominator, exclusion count, unknown count and degradation reasons
/// (GCPC-002), plus an explicit evaluation state so a metric whose recognizable population is empty is
/// published as <c>not_applicable</c> with a stated reason rather than a satisfied ratio (GCPC-009).
/// </summary>
public sealed record CoverageMetricDto
{
    private const string EvaluatedState = "evaluated";
    private const string NotApplicableState = "not_applicable";

    public string State { get; }

    public string? NotApplicableReason { get; }

    public int Numerator { get; }

    public int Denominator { get; }

    public int Exclusions { get; }

    public int Unknowns { get; }

    public ImmutableArray<DegradationReasonDto> DegradationReasons { get; }

    public CoverageMetricDto(
        string state,
        string? notApplicableReason,
        int numerator,
        int denominator,
        int exclusions,
        int unknowns,
        ImmutableArray<DegradationReasonDto> degradationReasons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (state is not (EvaluatedState or NotApplicableState))
        {
            throw new ArgumentException(
                $"'{state}' is not a valid coverage evaluation state. Allowed values: '{EvaluatedState}', '{NotApplicableState}'.",
                nameof(state));
        }

        if (state == NotApplicableState && string.IsNullOrWhiteSpace(notApplicableReason))
        {
            throw new ArgumentException(
                "A 'not_applicable' coverage metric must state its reason.", nameof(notApplicableReason));
        }

        if (state == EvaluatedState && notApplicableReason is not null)
        {
            throw new ArgumentException(
                "An 'evaluated' coverage metric must not carry a not_applicable reason.", nameof(notApplicableReason));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(numerator);
        ArgumentOutOfRangeException.ThrowIfNegative(denominator);
        ArgumentOutOfRangeException.ThrowIfNegative(exclusions);
        ArgumentOutOfRangeException.ThrowIfNegative(unknowns);

        State = state;
        NotApplicableReason = notApplicableReason;
        Numerator = numerator;
        Denominator = denominator;
        Exclusions = exclusions;
        Unknowns = unknowns;
        DegradationReasons = degradationReasons.IsDefault ? ImmutableArray<DegradationReasonDto>.Empty : degradationReasons;
    }

    public static CoverageMetricDto Evaluated(
        int numerator,
        int denominator,
        int exclusions,
        int unknowns,
        ImmutableArray<DegradationReasonDto> degradationReasons = default) =>
        new(EvaluatedState, null, numerator, denominator, exclusions, unknowns, degradationReasons);

    public static CoverageMetricDto NotApplicable(
        string reason, ImmutableArray<DegradationReasonDto> degradationReasons = default) =>
        new(NotApplicableState, reason, 0, 0, 0, 0, degradationReasons);
}

public sealed record CoverageEnvelope(
    CoverageMetricDto EntryPointCoverage,
    CoverageMetricDto LinkedCallCoverage,
    CoverageMetricDto ContractCoverage,
    CoverageMetricDto PersistenceCoverage);

/// <summary>
/// The run's certification status. <c>not_evaluated</c> has left the vocabulary (GCPC-001): only
/// <c>passed</c>, <c>degraded</c> or <c>failed</c> can be constructed.
/// </summary>
public sealed record RunCertificationEnvelope
{
    private static readonly ImmutableHashSet<string> AllowedStatuses =
        ImmutableHashSet.Create(StringComparer.Ordinal, "passed", "degraded", "failed");

    public string Status { get; }

    public ImmutableArray<string> Reasons { get; }

    public RunCertificationEnvelope(string status, ImmutableArray<string> reasons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!AllowedStatuses.Contains(status))
        {
            throw new ArgumentException(
                $"'{status}' is not a valid run-certification status. Allowed values: 'passed', 'degraded', 'failed'.",
                nameof(status));
        }

        Status = status;
        Reasons = reasons.IsDefault ? ImmutableArray<string>.Empty : reasons;
    }
}

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
