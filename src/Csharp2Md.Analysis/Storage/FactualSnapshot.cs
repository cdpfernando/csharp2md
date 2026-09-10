using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Storage;

/// <summary>
/// A coverage metric's evaluation state (GCPC-009): <see cref="Evaluated"/> when the metric's
/// recognizable population is non-empty, <see cref="NotApplicable"/> when it is empty -- a metric is
/// never published as a satisfied ratio over an empty population.
/// </summary>
public enum CoverageMetricState
{
    Evaluated,
    NotApplicable,
}

/// <summary>One degradation reason recorded against a coverage metric (GCPC-004).</summary>
public sealed record DegradationReason
{
    public string Code { get; }

    public string Detail { get; }

    public int AffectedCount { get; }

    public DegradationReason(string code, string detail, int affectedCount)
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
/// (GCPC-002), or an explicit <see cref="CoverageMetricState.NotApplicable"/> state with a stated reason
/// when the metric's recognizable population is empty (GCPC-009).
/// </summary>
public sealed record CoverageMetric
{
    public CoverageMetricState State { get; }

    public string? NotApplicableReason { get; }

    public int Numerator { get; }

    public int Denominator { get; }

    public int Exclusions { get; }

    public int Unknowns { get; }

    public ImmutableArray<DegradationReason> DegradationReasons { get; }

    internal CoverageMetric(
        CoverageMetricState state,
        string? notApplicableReason,
        int numerator,
        int denominator,
        int exclusions,
        int unknowns,
        ImmutableArray<DegradationReason> degradationReasons)
    {
        if (state == CoverageMetricState.NotApplicable && string.IsNullOrWhiteSpace(notApplicableReason))
        {
            throw new ArgumentException(
                "A 'not_applicable' coverage metric must state its reason.", nameof(notApplicableReason));
        }

        if (state == CoverageMetricState.Evaluated && notApplicableReason is not null)
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
        DegradationReasons = degradationReasons.IsDefault ? ImmutableArray<DegradationReason>.Empty : degradationReasons;
    }

    public static CoverageMetric Evaluated(
        int numerator,
        int denominator,
        int exclusions,
        int unknowns,
        ImmutableArray<DegradationReason> degradationReasons = default) =>
        new(CoverageMetricState.Evaluated, null, numerator, denominator, exclusions, unknowns, degradationReasons);

    public static CoverageMetric NotApplicable(
        string reason, ImmutableArray<DegradationReason> degradationReasons = default) =>
        new(CoverageMetricState.NotApplicable, reason, 0, 0, 0, 0, degradationReasons);
}

/// <summary>The four mandatory coverage metrics (GCPC-002, GCPC-003).</summary>
public sealed record CoverageReport(
    CoverageMetric EntryPointCoverage,
    CoverageMetric LinkedCallCoverage,
    CoverageMetric ContractCoverage,
    CoverageMetric PersistenceCoverage);

/// <summary>The run's certification status. <c>not_evaluated</c> is not a member (GCPC-001).</summary>
public enum RunCertificationStatus
{
    Passed,
    Degraded,
    Failed,
}

/// <summary>The run-certification status and the reasons that produced it (GCPC-001, GCPC-006..GCPC-010).</summary>
public sealed record RunCertificationReport(RunCertificationStatus Status, ImmutableArray<string> Reasons);

/// <summary>One exclusion category's count (GCPC-016), reused by both accounting reports.</summary>
public sealed record ExclusionCategoryCount(string Category, int Count);

/// <summary>
/// Per-disposition totals for recognized invocation occurrences (GCPC-011, GCPC-012), summing exactly to
/// <see cref="RecognizedOccurrences"/> (GCPC-015), plus the occurrences (if any) that carry no disposition
/// at all -- their presence forces run certification to <c>failed</c> (GCPC-013).
/// </summary>
public sealed record InvocationAccountingReport(
    int RecognizedOccurrences,
    int Confirmed,
    int Candidate,
    int Unresolved,
    int OpenFrontier,
    ImmutableArray<ExclusionCategoryCount> Exclusions,
    ImmutableArray<string> UnaccountedOccurrences);

/// <summary>
/// Per-outcome totals for recognized message operations and boundary payload slots (GCPC-087, GCPC-088),
/// summing exactly to <see cref="RecognizedTotal"/> -- the absence of a contract is never presented as the
/// absence of messaging (GCPC-089), since every recognized operation is counted here regardless of outcome.
/// </summary>
public sealed record ContractAccountingReport(
    int RecognizedTotal,
    int Contracted,
    int Candidate,
    int Unresolved,
    ImmutableArray<ExclusionCategoryCount> Exclusions);

public sealed record FactualSnapshot
{
    public static FactualSnapshot Empty { get; } = new(
        ImmutableArray<IFact>.Empty,
        ImmutableArray<Observation>.Empty,
        ImmutableArray<ConfirmedRelation>.Empty,
        ImmutableArray<CandidateLink>.Empty,
        ImmutableArray<UnresolvedRecord>.Empty,
        ImmutableArray<OpenFrontier>.Empty);

    public ImmutableArray<IFact> Facts { get; }

    public ImmutableArray<Observation> Observations { get; }

    public ImmutableArray<ConfirmedRelation> ConfirmedRelations { get; }

    public ImmutableArray<CandidateLink> Candidates { get; }

    public ImmutableArray<UnresolvedRecord> Unresolved { get; }

    public ImmutableArray<OpenFrontier> Frontiers { get; }

    public ImmutableArray<DiagnosticRecord> Diagnostics { get; }

    public ImmutableArray<SuspectedSecretEvidence> SuspectedSecrets { get; }

    public DocumentPolicyReport DocumentPolicy { get; }

    /// <summary>
    /// The run's coverage report (AD-024), or <see langword="null"/> before the Validation and Coverage
    /// stage has run.
    /// </summary>
    public CoverageReport? Coverage { get; }

    /// <summary>
    /// The run's certification report (AD-024), or <see langword="null"/> before the Validation and
    /// Coverage stage has run.
    /// </summary>
    public RunCertificationReport? Certification { get; }

    /// <summary>The invocation-accounting ledger (AD-024, GCPC-012), or <see langword="null"/> before it is computed.</summary>
    public InvocationAccountingReport? InvocationAccounting { get; }

    /// <summary>The contract-accounting ledger (AD-024, GCPC-088), or <see langword="null"/> before it is computed.</summary>
    public ContractAccountingReport? ContractAccounting { get; }

    public FactualSnapshot(
        ImmutableArray<IFact> facts,
        ImmutableArray<Observation> observations,
        ImmutableArray<ConfirmedRelation> confirmedRelations,
        ImmutableArray<CandidateLink> candidates,
        ImmutableArray<UnresolvedRecord> unresolved,
        ImmutableArray<OpenFrontier> frontiers,
        ImmutableArray<DiagnosticRecord> diagnostics = default,
        ImmutableArray<SuspectedSecretEvidence> suspectedSecrets = default,
        DocumentPolicyReport? documentPolicy = null,
        CoverageReport? coverage = null,
        RunCertificationReport? certification = null,
        InvocationAccountingReport? invocationAccounting = null,
        ContractAccountingReport? contractAccounting = null)
    {
        Facts = facts;
        Observations = observations;
        ConfirmedRelations = confirmedRelations;
        Candidates = candidates;
        Unresolved = unresolved;
        Frontiers = frontiers;
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<DiagnosticRecord>.Empty : diagnostics;
        SuspectedSecrets = suspectedSecrets.IsDefault
            ? ImmutableArray<SuspectedSecretEvidence>.Empty
            : suspectedSecrets;
        DocumentPolicy = documentPolicy ?? DocumentPolicyReport.Empty;
        Coverage = coverage;
        Certification = certification;
        InvocationAccounting = invocationAccounting;
        ContractAccounting = contractAccounting;
    }

    public FactualSnapshot Merge(FactualSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new FactualSnapshot(
            Facts.AddRange(other.Facts),
            Observations.AddRange(other.Observations),
            ConfirmedRelations.AddRange(other.ConfirmedRelations),
            Candidates.AddRange(other.Candidates),
            Unresolved.AddRange(other.Unresolved),
            Frontiers.AddRange(other.Frontiers),
            Diagnostics.AddRange(other.Diagnostics),
            SuspectedSecrets.AddRange(other.SuspectedSecrets),
            DocumentPolicy.Merge(other.DocumentPolicy),
            // Coverage, certification and the accounting ledgers are singleton, run-level results (never
            // per-document lists), so merging takes whichever side actually carries a value rather than
            // combining the two -- summing them would double count a single run's totals. The other
            // snapshot wins when both sides carry one, matching Session.Stage's merge-in-order semantics.
            other.Coverage ?? Coverage,
            other.Certification ?? Certification,
            other.InvocationAccounting ?? InvocationAccounting,
            other.ContractAccounting ?? ContractAccounting);
    }
}
