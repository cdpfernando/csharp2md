using System.Reflection;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

/// <summary>
/// GCPC-001, GCPC-002: <see cref="DomainMapper"/> publishes the coverage and run-certification values
/// <c>ValidationAndCoverageStage</c> actually computed, rather than a hardcoded zero/not-yet-computed
/// placeholder.
/// </summary>
[Collection(FilesystemStoreCollection.Name)]
public sealed class CoverageAndCertificationPublicationTests
{
    private const string SolutionKey = @"C:\src\Acme Widgets.sln";

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    [Trait("Requirement", "GCPC-002")]
    public void Commit_SnapshotWithComputedCoverageAndCertification_PublishesThoseValuesToDisk()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);

        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(SnapshotWithComputedEnvelopes());
        session.Commit();

        var coverageBytes = File.ReadAllBytes(Path.Combine(child, "coverage.json"));
        var coverage = CanonicalJson.Read<CoverageEnvelope>(coverageBytes);
        Assert.Equal("evaluated", coverage.EntryPointCoverage.State);
        Assert.Equal(3, coverage.EntryPointCoverage.Numerator);
        Assert.Equal(5, coverage.EntryPointCoverage.Denominator);
        Assert.Equal(1, coverage.EntryPointCoverage.Unknowns);
        Assert.Equal("not_applicable", coverage.PersistenceCoverage.State);
        Assert.Equal("No recognized data-access occurrences were found.", coverage.PersistenceCoverage.NotApplicableReason);

        var certificationBytes = File.ReadAllBytes(Path.Combine(child, "run-certification.json"));
        var certification = CanonicalJson.Read<RunCertificationEnvelope>(certificationBytes);
        Assert.Equal("degraded", certification.Status);
        Assert.Contains(
            certification.Reasons,
            reason => reason.Contains("entry_point_coverage", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-001")]
    public void DomainMapper_HasNoHardcodedZeroOrUnevaluatedCoverageField()
    {
        var members = typeof(DomainMapper).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        Assert.DoesNotContain(members, field => field.Name.Contains("ZeroCoverage", StringComparison.Ordinal));
        Assert.DoesNotContain(members, field => field.Name.Contains("not_evaluated", StringComparison.OrdinalIgnoreCase));
    }

    private static FactualSnapshot SnapshotWithComputedEnvelopes()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));

        var entryPointCoverage = CoverageMetric.Evaluated(numerator: 3, denominator: 5, exclusions: 1, unknowns: 1);
        var evaluatedClean = CoverageMetric.Evaluated(numerator: 2, denominator: 2, exclusions: 0, unknowns: 0);
        var persistenceCoverage = CoverageMetric.NotApplicable("No recognized data-access occurrences were found.");
        var coverage = new CoverageReport(entryPointCoverage, evaluatedClean, evaluatedClean, persistenceCoverage);

        var certification = new RunCertificationReport(
            RunCertificationStatus.Degraded,
            ["entry_point_coverage has 1 unknown occurrence(s)"]);

        return new FactualSnapshot(
            [solution],
            [],
            [],
            [],
            [],
            [],
            coverage: coverage,
            certification: certification);
    }
}
