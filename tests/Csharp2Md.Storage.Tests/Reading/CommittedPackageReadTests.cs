using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Tests.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Reading;

public sealed class CommittedPackageReadTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-34")]
    public void Read_EmptyCommittedPackage_EqualsFactualSnapshotEmpty()
    {
        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath, FactualSnapshot.Empty);

        var result = FactualPackageReader.Read(package);

        Assert.Equal(FactualSnapshot.Empty, result.Snapshot);
        Assert.True(result.Snapshot.Facts.IsEmpty);
        Assert.True(result.Snapshot.Observations.IsEmpty);
        Assert.True(result.Snapshot.ConfirmedRelations.IsEmpty);
        Assert.True(result.Snapshot.Candidates.IsEmpty);
        Assert.True(result.Snapshot.Unresolved.IsEmpty);
        Assert.True(result.Snapshot.Frontiers.IsEmpty);
        AssertHappyPathEnvelopes(result);
    }

    [Theory]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-34")]
    [MemberData(nameof(FactRoundTripTests.FactFixtures), MemberType = typeof(FactRoundTripTests))]
    public void Read_CommittedFactFixture_EqualsOriginalUnderDomainEquality(string factType, FactualSnapshot snapshot)
    {
        Assert.Equal(factType, snapshot.Facts[0].Reference.FactType);

        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath, snapshot);

        var result = FactualPackageReader.Read(package);

        AssertDomainEqual(snapshot, result.Snapshot);
        AssertHappyPathEnvelopes(result);
    }

    [Theory]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-34")]
    [MemberData(nameof(ObservationRelationRoundTripTests.ObservationKinds), MemberType = typeof(ObservationRelationRoundTripTests))]
    public void Read_CommittedObservationKind_EqualsOriginalUnderDomainEquality(ObservationKind kind)
    {
        var snapshot = new FactualSnapshot([], [CreateObservation(kind)], [], [], [], []);

        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath, snapshot);

        var result = FactualPackageReader.Read(package);

        AssertDomainEqual(snapshot, result.Snapshot);
        AssertHappyPathEnvelopes(result);
    }

    [Fact]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-34")]
    public void Read_CommittedConfirmedRelation_EqualsOriginalUnderDomainEquality()
    {
        var snapshot = new FactualSnapshot([], [], [CreateContainsRelation()], [], [], []);

        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath, snapshot);

        var result = FactualPackageReader.Read(package);

        AssertDomainEqual(snapshot, result.Snapshot);
        AssertHappyPathEnvelopes(result);
    }

    [Fact]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-34")]
    public void Read_CommittedCandidateLink_EqualsOriginalUnderDomainEquality()
    {
        var snapshot = new FactualSnapshot(
            [],
            [],
            [],
            [CandidateLink.Create(RelationKind.Contains, Solution.Create(AcmeSolution).Reference, Project.Create(AcmeProject).Reference, ValidEvidence())],
            [],
            []);

        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath, snapshot);

        var result = FactualPackageReader.Read(package);

        AssertDomainEqual(snapshot, result.Snapshot);
        AssertHappyPathEnvelopes(result);
    }

    [Fact]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-34")]
    public void Read_CommittedUnresolvedRecord_EqualsOriginalUnderDomainEquality()
    {
        var snapshot = new FactualSnapshot(
            [],
            [],
            [],
            [],
            [UnresolvedRecord.Create(RelationKind.Contains, Solution.Create(AcmeSolution).Reference, UnresolvedCause.AmbiguousTarget, ValidEvidence())],
            []);

        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath, snapshot);

        var result = FactualPackageReader.Read(package);

        AssertDomainEqual(snapshot, result.Snapshot);
        AssertHappyPathEnvelopes(result);
    }

    [Fact]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-34")]
    public void Read_CommittedOpenFrontier_EqualsOriginalUnderDomainEquality()
    {
        var snapshot = new FactualSnapshot(
            [],
            [],
            [],
            [],
            [],
            [OpenFrontier.Create(ValidOccurrence(), FrontierCause.FurtherContinuationObserved)]);

        using var output = TempOutputRoot.Create();
        var package = Commit(output.DirectoryPath, snapshot);

        var result = FactualPackageReader.Read(package);

        AssertDomainEqual(snapshot, result.Snapshot);
        AssertHappyPathEnvelopes(result);
    }

    private static string Commit(string outputRoot, FactualSnapshot snapshot)
    {
        var store = new FilesystemTransactionalStore(outputRoot);
        var session = store.Open(SolutionKey);
        session.Stage(snapshot);
        session.Commit();
        return FilesystemTestPaths.ChildDirectory(outputRoot, SolutionKey);
    }

    private static void AssertDomainEqual(FactualSnapshot expected, FactualSnapshot actual)
    {
        Assert.Equal(expected.Facts.Length, actual.Facts.Length);
        for (var index = 0; index < expected.Facts.Length; index++)
        {
            Assert.Equal(expected.Facts[index], actual.Facts[index]);
        }

        Assert.Equal(expected.Observations.Length, actual.Observations.Length);
        for (var index = 0; index < expected.Observations.Length; index++)
        {
            AssertObservationEqual(expected.Observations[index], actual.Observations[index]);
        }

        Assert.Equal(expected.ConfirmedRelations.Length, actual.ConfirmedRelations.Length);
        for (var index = 0; index < expected.ConfirmedRelations.Length; index++)
        {
            Assert.Equal(expected.ConfirmedRelations[index], actual.ConfirmedRelations[index]);
        }

        Assert.Equal(expected.Candidates.Length, actual.Candidates.Length);
        for (var index = 0; index < expected.Candidates.Length; index++)
        {
            Assert.Equal(expected.Candidates[index], actual.Candidates[index]);
        }

        Assert.Equal(expected.Unresolved.Length, actual.Unresolved.Length);
        for (var index = 0; index < expected.Unresolved.Length; index++)
        {
            Assert.Equal(expected.Unresolved[index], actual.Unresolved[index]);
        }

        Assert.Equal(expected.Frontiers.Length, actual.Frontiers.Length);
        for (var index = 0; index < expected.Frontiers.Length; index++)
        {
            Assert.Equal(expected.Frontiers[index], actual.Frontiers[index]);
        }
    }

    private static void AssertHappyPathEnvelopes(PackageReadResult result)
    {
        Assert.True(result.Quarantine.IsEmpty);
        Assert.Equal("not_evaluated", result.Certification.Status);
        AssertZero(result.Coverage.EntryPointCoverage);
        AssertZero(result.Coverage.LinkedCallCoverage);
        AssertZero(result.Coverage.ContractCoverage);
        AssertZero(result.Coverage.PersistenceCoverage);
    }

    private static void AssertZero(CoverageMetricDto metric)
    {
        Assert.Equal(0, metric.Numerator);
        Assert.Equal(0, metric.Denominator);
        Assert.Equal(0, metric.Exclusions);
        Assert.Equal(0, metric.Unknowns);
        Assert.True(metric.DegradationReasons.IsEmpty);
    }

    private static void AssertObservationEqual(Observation expected, Observation actual)
    {
        Assert.Equal(expected.Identity, actual.Identity);
        Assert.Equal(expected.Locator, actual.Locator);
        Assert.Equal(expected.ExtractionMethod, actual.ExtractionMethod);
        Assert.Equal(expected.Diagnostic, actual.Diagnostic);
        Assert.Equal(expected.DocumentHash, actual.DocumentHash);
        Assert.Equal(expected.ExtractorVersion, actual.ExtractorVersion);
    }

    private static Observation CreateObservation(ObservationKind kind) =>
        Observation.Create(
            Solution.Create(AcmeSolution).Reference,
            kind,
            NormalizedPayload.Create([]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));

    private static ConfirmedRelation CreateContainsRelation() =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            Solution.Create(AcmeSolution).Reference,
            Project.Create(AcmeProject).Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            ValidEvidence(),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create([ValidOccurrence()]);

    private static ObservationIdentity ValidOccurrence() =>
        new(Solution.Create(AcmeSolution).Reference, ObservationKind.Invocation, NormalizedPayload.Create([]), 1);

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");
}
