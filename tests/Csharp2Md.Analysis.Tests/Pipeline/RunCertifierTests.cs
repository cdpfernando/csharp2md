using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

/// <summary>
/// GCPC-001, GCPC-006..GCPC-010, GCPC-013: the run-certification status <see cref="RunCertifier"/>
/// computes from the four coverage metrics and the invocation-accounting ledger.
/// </summary>
public sealed class RunCertifierTests
{
    private static readonly CoverageMetric CleanEvaluatedMetric = CoverageMetric.Evaluated(5, 5, 0, 0);

    [Fact]
    [Trait("Requirement", "GCPC-006")]
    public void Certify_AllMetricsEvaluatedWithNoDegradationNoQuarantineNoUnaccounted_IsPassed()
    {
        var coverage = new CoverageReport(
            CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric);

        var report = RunCertifier.Certify(coverage, invocationAccounting: null, structuralCorruption: false, collidingIdentity: null);

        Assert.Equal(RunCertificationStatus.Passed, report.Status);
        Assert.Empty(report.Reasons);
    }

    [Fact]
    [Trait("Requirement", "GCPC-007")]
    public void Certify_AMetricCarriesAnUnknownOccurrence_IsDegraded()
    {
        var withUnknown = CoverageMetric.Evaluated(4, 5, 0, 1);
        var coverage = new CoverageReport(withUnknown, CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric);

        var report = RunCertifier.Certify(coverage, invocationAccounting: null, structuralCorruption: false, collidingIdentity: null);

        Assert.Equal(RunCertificationStatus.Degraded, report.Status);
        Assert.NotEmpty(report.Reasons);
    }

    [Fact]
    [Trait("Requirement", "GCPC-008")]
    public void Certify_QuarantinedDerivedFact_IsFailed()
    {
        var coverage = new CoverageReport(
            CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric);

        var report = RunCertifier.Certify(
            coverage, invocationAccounting: null, structuralCorruption: true, collidingIdentity: "id1:fact;colliding=true");

        Assert.Equal(RunCertificationStatus.Failed, report.Status);
        Assert.Contains(report.Reasons, reason => reason.Contains("id1:fact;colliding=true", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-013")]
    public void Certify_UnaccountedInvocationOccurrence_ForcesFailedAndNamesTheOccurrence()
    {
        var coverage = new CoverageReport(
            CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric);
        var invocationAccounting = new InvocationAccountingReport(
            RecognizedOccurrences: 5,
            Confirmed: 4,
            Candidate: 0,
            Unresolved: 0,
            OpenFrontier: 0,
            Exclusions: [],
            UnaccountedOccurrences: ["owner#Invocation#1"]);

        var report = RunCertifier.Certify(
            coverage, invocationAccounting, structuralCorruption: false, collidingIdentity: null);

        Assert.Equal(RunCertificationStatus.Failed, report.Status);
        Assert.Contains(report.Reasons, reason => reason.Contains("owner#Invocation#1", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    [Trait("Requirement", "GCPC-010")]
    public void Certify_LibraryOnlySolutionWithNoEntryPoints_IsDegradedNeverPassed()
    {
        var noEntryPoints = CoverageMetric.NotApplicable("No framework-recognizable entry-point candidates were found in the analyzed variants.");
        var coverage = new CoverageReport(noEntryPoints, CleanEvaluatedMetric, CleanEvaluatedMetric, CleanEvaluatedMetric);

        var report = RunCertifier.Certify(coverage, invocationAccounting: null, structuralCorruption: false, collidingIdentity: null);

        Assert.Equal(RunCertificationStatus.Degraded, report.Status);
        Assert.NotEqual(RunCertificationStatus.Passed, report.Status);
        Assert.Contains(
            report.Reasons,
            reason => reason.Contains("entry_point_coverage", StringComparison.Ordinal)
                && reason.Contains("not_applicable", StringComparison.Ordinal)
                && reason.Contains("No framework-recognizable entry-point candidates", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-010")]
    public void Certify_EveryMandatoryMetricIsNotApplicable_IsDegradedNeverPassed()
    {
        var notApplicable = CoverageMetric.NotApplicable("No recognizable population was found.");
        var coverage = new CoverageReport(notApplicable, notApplicable, notApplicable, notApplicable);

        var report = RunCertifier.Certify(coverage, invocationAccounting: null, structuralCorruption: false, collidingIdentity: null);

        Assert.Equal(RunCertificationStatus.Degraded, report.Status);
    }

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void RunCertificationStatus_HasNoNotEvaluatedMember()
    {
        var names = Enum.GetNames<RunCertificationStatus>();

        Assert.DoesNotContain(names, name => name.Contains("NotEvaluated", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, names.Length);
    }
}
