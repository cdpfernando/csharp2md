using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class FactualSnapshotTests
{
    [Fact]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "ROSE-58")]
    public void Empty_HasLengthZeroArraysForEveryFamily()
    {
        var empty = FactualSnapshot.Empty;

        Assert.False(empty.Facts.IsDefault);
        Assert.Empty(empty.Facts);
        Assert.False(empty.Observations.IsDefault);
        Assert.Empty(empty.Observations);
        Assert.False(empty.ConfirmedRelations.IsDefault);
        Assert.Empty(empty.ConfirmedRelations);
        Assert.False(empty.Candidates.IsDefault);
        Assert.Empty(empty.Candidates);
        Assert.False(empty.Unresolved.IsDefault);
        Assert.Empty(empty.Unresolved);
        Assert.False(empty.Frontiers.IsDefault);
        Assert.Empty(empty.Frontiers);
        Assert.False(empty.Diagnostics.IsDefault);
        Assert.Empty(empty.Diagnostics);
        Assert.False(empty.SuspectedSecrets.IsDefault);
        Assert.Empty(empty.SuspectedSecrets);
    }

    [Fact]
    [Trait("Requirement", "STOR-10")]
    public void Merge_SharedSolutionIdentity_DoesNotThrowAndConcatenatesFacts()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));

        var left = SnapshotWithFact(solution);
        var right = SnapshotWithFact(solution);

        var merged = left.Merge(right);

        Assert.Equal(2, merged.Facts.Length);
        Assert.Equal(solution, merged.Facts[0]);
        Assert.Equal(solution, merged.Facts[1]);
        Assert.Empty(merged.Observations);
        Assert.Empty(merged.ConfirmedRelations);
        Assert.Empty(merged.Candidates);
        Assert.Empty(merged.Unresolved);
        Assert.Empty(merged.Frontiers);
        Assert.Empty(merged.Diagnostics);
        Assert.Empty(merged.SuspectedSecrets);
    }

    [Fact]
    [Trait("Requirement", "ROSE-58")]
    public void SixArgumentConstructor_StillCompilesAndLeavesDiagnosticsAndSecretsEmpty()
    {
        var snapshot = new FactualSnapshot(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty);

        Assert.Empty(snapshot.Diagnostics);
        Assert.Empty(snapshot.SuspectedSecrets);
    }

    [Fact]
    [Trait("Requirement", "ROSE-58")]
    public void Merge_ConcatenatesDiagnosticsAndSuspectedSecrets()
    {
        var leftDiagnostic = new DiagnosticRecord("missing-project", "Acme.DoesNotExist is absent.", "Acme.DoesNotExist/Acme.DoesNotExist.csproj");
        var rightDiagnostic = new DiagnosticRecord("compilation-error", "Acme.Broken produced error diagnostics.", "Acme.Broken/Acme.Broken.csproj");
        var leftSecret = Secret("src/Left.cs", "Password=***");
        var rightSecret = Secret("src/Right.cs", "token=[REDACTED]");

        var left = new FactualSnapshot(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty,
            ImmutableArray.Create(leftDiagnostic),
            ImmutableArray.Create(leftSecret));
        var right = new FactualSnapshot(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty,
            ImmutableArray.Create(rightDiagnostic),
            ImmutableArray.Create(rightSecret));

        var merged = left.Merge(right);

        Assert.Equal(2, merged.Diagnostics.Length);
        Assert.Equal(leftDiagnostic, merged.Diagnostics[0]);
        Assert.Equal(rightDiagnostic, merged.Diagnostics[1]);
        Assert.Equal(2, merged.SuspectedSecrets.Length);
        Assert.Equal(leftSecret, merged.SuspectedSecrets[0]);
        Assert.Equal(rightSecret, merged.SuspectedSecrets[1]);
    }

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void SixArgumentConstructor_LeavesCoverageAndCertificationSlotsNull()
    {
        var snapshot = new FactualSnapshot(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty);

        Assert.Null(snapshot.Coverage);
        Assert.Null(snapshot.Certification);
        Assert.Null(snapshot.InvocationAccounting);
        Assert.Null(snapshot.ContractAccounting);
    }

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void Merge_OneSideCarriesCoverageAndCertification_TheOtherIsEmpty_ResultIsNotDoubled()
    {
        var coverage = new CoverageReport(
            CoverageMetric.Evaluated(1, 2, 0, 1),
            CoverageMetric.Evaluated(1, 1, 0, 0),
            CoverageMetric.NotApplicable("No message operations were recognized."),
            CoverageMetric.NotApplicable("No data-access operations were recognized."));
        var certification = new RunCertificationReport(RunCertificationStatus.Passed, []);
        var accounting = new InvocationAccountingReport(3, 1, 1, 1, 0, [], []);
        var contractAccounting = new ContractAccountingReport(2, 1, 0, 1, []);

        var populated = EmptySnapshotWith(coverage, certification, accounting, contractAccounting);
        var empty = FactualSnapshot.Empty;

        var mergedRight = populated.Merge(empty);
        var mergedLeft = empty.Merge(populated);

        // Neither merge direction fabricates a second copy of the run-level result: the coverage
        // numerator/denominator are exactly the source values, never summed with an empty side's zeros
        // doubling them, and the same instance survives regardless of merge order.
        Assert.Same(coverage, mergedRight.Coverage);
        Assert.Same(certification, mergedRight.Certification);
        Assert.Same(accounting, mergedRight.InvocationAccounting);
        Assert.Same(contractAccounting, mergedRight.ContractAccounting);
        Assert.Equal(1, mergedRight.Coverage!.EntryPointCoverage.Numerator);
        Assert.Equal(2, mergedRight.Coverage!.EntryPointCoverage.Denominator);

        Assert.Same(coverage, mergedLeft.Coverage);
        Assert.Same(certification, mergedLeft.Certification);
        Assert.Same(accounting, mergedLeft.InvocationAccounting);
        Assert.Same(contractAccounting, mergedLeft.ContractAccounting);
    }

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void Merge_BothSidesCarryCoverage_OtherSideWinsRatherThanSumming()
    {
        var left = new CoverageReport(
            CoverageMetric.Evaluated(1, 2, 0, 1),
            CoverageMetric.Evaluated(1, 1, 0, 0),
            CoverageMetric.NotApplicable("No message operations were recognized."),
            CoverageMetric.NotApplicable("No data-access operations were recognized."));
        var right = new CoverageReport(
            CoverageMetric.Evaluated(5, 5, 0, 0),
            CoverageMetric.Evaluated(1, 1, 0, 0),
            CoverageMetric.NotApplicable("No message operations were recognized."),
            CoverageMetric.NotApplicable("No data-access operations were recognized."));

        var merged = EmptySnapshotWith(left, null, null, null)
            .Merge(EmptySnapshotWith(right, null, null, null));

        // A double-counting bug would sum the two numerators (1 + 5 = 6) or the two denominators
        // (2 + 5 = 7); this asserts the merge instead takes the second snapshot's report whole.
        Assert.Same(right, merged.Coverage);
        Assert.Equal(5, merged.Coverage!.EntryPointCoverage.Numerator);
        Assert.Equal(5, merged.Coverage!.EntryPointCoverage.Denominator);
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    public void CoverageMetric_NotApplicable_RequiresAReason()
    {
        Assert.Throws<ArgumentException>(() => CoverageMetric.NotApplicable(""));
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    public void CoverageMetric_Evaluated_CannotCarryANotApplicableReason()
    {
        Assert.Throws<ArgumentException>(
            () => new CoverageMetric(CoverageMetricState.Evaluated, "unexpected", 1, 1, 0, 0, []));
    }

    [Fact]
    [Trait("Requirement", "GCPC-009")]
    public void CoverageMetric_NotApplicableState_RequiresAReasonThroughTheConstructorToo()
    {
        Assert.Throws<ArgumentException>(
            () => new CoverageMetric(CoverageMetricState.NotApplicable, null, 0, 0, 0, 0, []));
    }

    private static FactualSnapshot EmptySnapshotWith(
        CoverageReport? coverage,
        RunCertificationReport? certification,
        InvocationAccountingReport? invocationAccounting,
        ContractAccountingReport? contractAccounting) =>
        new(
            ImmutableArray<IFact>.Empty,
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty,
            coverage: coverage,
            certification: certification,
            invocationAccounting: invocationAccounting,
            contractAccounting: contractAccounting);

    private static FactualSnapshot SnapshotWithFact(IFact fact) =>
        new(
            ImmutableArray.Create(fact),
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty);

    private static SuspectedSecretEvidence Secret(string document, string excerpt) =>
        SuspectedSecretEvidence.Create(
            DocumentId.Create(document),
            new SourceSpan(1, 1, 1, 8),
            DocumentHash.Create(new string('a', 64)),
            RedactedExcerpt.Create(excerpt));
}
