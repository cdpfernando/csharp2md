using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Pipeline;

/// <summary>
/// Computes the run-certification status (GCPC-001, GCPC-006..GCPC-010, GCPC-013) from the four coverage
/// metrics T27/T28 computed and the invocation-accounting ledger T29 published. <c>not_evaluated</c> is
/// never a possible outcome (GCPC-001): every run publishes exactly one of <c>passed</c>, <c>degraded</c>
/// or <c>failed</c>.
/// </summary>
/// <remarks>
/// A "classifier conflict" (taxonomy.md: "the conflict is published, and certification fails when the
/// conflict affects supported coverage") has no dedicated published record anywhere in this codebase
/// today -- the only fact-level conflict this pipeline actually detects and publishes is an identity
/// collision, surfaced as <c>SnapshotAccumulator.StructuralCorruption</c>/<c>CollidingIdentity</c>. This
/// certifier therefore treats that single signal as covering both "a derived fact is quarantined" and "a
/// classifier conflict affects a covered area" in GCPC-006/007/008; there is nothing else to check
/// against, and no additional exclusion category is invented for it.
/// </remarks>
internal static class RunCertifier
{
    public static RunCertificationReport Certify(
        CoverageReport coverage,
        InvocationAccountingReport? invocationAccounting,
        bool structuralCorruption,
        string? collidingIdentity)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        var reasons = new List<string>();

        var unaccounted = invocationAccounting?.UnaccountedOccurrences ?? ImmutableArray<string>.Empty;
        if (structuralCorruption)
        {
            reasons.Add($"quarantined derived fact: colliding identity '{collidingIdentity}'");
        }

        foreach (var occurrence in unaccounted)
        {
            reasons.Add($"unaccounted invocation occurrence: '{occurrence}' carries no disposition");
        }

        if (reasons.Count > 0)
        {
            return new RunCertificationReport(RunCertificationStatus.Failed, [.. reasons]);
        }

        var metrics = new (string Name, CoverageMetric Metric)[]
        {
            ("entry_point_coverage", coverage.EntryPointCoverage),
            ("linked_call_coverage", coverage.LinkedCallCoverage),
            ("contract_coverage", coverage.ContractCoverage),
            ("persistence_coverage", coverage.PersistenceCoverage),
        };

        foreach (var (name, metric) in metrics)
        {
            if (metric.State == CoverageMetricState.NotApplicable)
            {
                reasons.Add($"{name} is not_applicable: {metric.NotApplicableReason}");
                continue;
            }

            if (metric.Unknowns > 0)
            {
                reasons.Add($"{name} has {metric.Unknowns} unknown occurrence(s)");
            }

            foreach (var degradation in metric.DegradationReasons)
            {
                reasons.Add($"{name}: {degradation.Code} ({degradation.Detail}), affecting {degradation.AffectedCount}");
            }
        }

        var status = reasons.Count > 0 ? RunCertificationStatus.Degraded : RunCertificationStatus.Passed;
        return new RunCertificationReport(status, [.. reasons]);
    }
}
