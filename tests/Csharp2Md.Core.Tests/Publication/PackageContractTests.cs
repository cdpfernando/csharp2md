using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class PackageContractTests
{
    [Fact]
    [Trait("Requirement", "PKG-01")]
    [Trait("Requirement", "PUB-02")]
    public void PackagePlan_ContainsRootManifestAndEveryFinalPayload()
    {
        var plan = Plan(Include(ManifestArtifact(), TableArtifact()));

        Assert.Contains(plan.Artifacts, artifact => artifact.Path.Value == "manifest.json");
        Assert.All(plan.Artifacts, artifact => Assert.False(artifact.Payload.IsDefaultOrEmpty));
        Assert.Equal("csharp2md.tokens.bytes-per-token-v1", plan.Manifest.TokenEstimator);
        Assert.Equal(4.0, plan.Manifest.TokenDivisor);
    }

    [Fact]
    [Trait("Requirement", "PUB-01")]
    public void PackagePlan_WithoutRootManifest_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => Plan(Include(TableArtifact())));
        Assert.Equal("artifacts", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "PUB-01")]
    public void PackagePlan_HasNoDeferredFragmentContract()
    {
        var deferred = typeof(PackagePlan).Assembly.GetTypes()
            .Where(type => type.Name.Contains("DeferredFragment", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(deferred.Length == 0, "Deferred fragment type(s) must not exist: " + string.Join(", ", deferred));
        Assert.Throws<ArgumentException>(() => Plan(Include(ManifestArtifact(), DeferredPayloadArtifact())));
    }

    [Fact]
    [Trait("Requirement", "PKG-08")]
    [Trait("Requirement", "STO-05")]
    public void Manifest_DeclaresIncludeTestsPolicySolutionsRootsIndexesAndJourneys()
    {
        var manifest = Manifest(includeTests: true);

        Assert.True(manifest.IncludeTests);
        var solution = Assert.Single(manifest.Solutions);
        Assert.Equal("src/Acme.sln", solution.LogicalRelativePath);
        Assert.Equal("indexes/roots.json", solution.Roots.EntryPath);
        Assert.Equal(1, solution.Roots.Count);
        Assert.Equal(8, solution.Indexes.Length);
        Assert.Contains(solution.Indexes, index => index.Kind == NavigationIndexKind.Identity);
        Assert.Equal(4, solution.Journeys.Length);
        Assert.Equal(
            [JourneyKind.Locate, JourneyKind.FollowFlow, JourneyKind.ReverseImpact, JourneyKind.EvidenceDisposition],
            solution.Journeys.Select(journey => journey.Kind).ToArray());
    }

    [Fact]
    [Trait("Requirement", "CRT-03")]
    public void PackagePlan_SeparatesExtractionAndPublicationMeasurements()
    {
        var measurements = new PublicationMeasurements(
            new ExtractionMeasurements(10, 4),
            publishedArtifactCount: 2,
            filteredByReason: ImmutableArray.Create(new FilteredCount("tests", 3)));

        Assert.Equal(10, measurements.Extraction.ExtractedCount);
        Assert.Equal(4, measurements.Extraction.FilteredCount);
        Assert.Equal(2, measurements.PublishedArtifactCount);
        Assert.Equal("tests", Assert.Single(measurements.FilteredByReason).Reason);
        Assert.Equal(3, Assert.Single(measurements.FilteredByReason).Count);
        Assert.NotEqual(measurements.Extraction.ExtractedCount, measurements.PublishedArtifactCount);
    }

    [Fact]
    [Trait("Requirement", "STO-06")]
    public void PlannedArtifact_CarriesFamilyRecordCountAndContentDigestWithPayload()
    {
        var artifact = TableArtifact();

        Assert.Equal(ArtifactFamily.Table, artifact.Family);
        Assert.Equal(2, artifact.RecordCount);
        Assert.Equal("digest-table", artifact.ContentDigest);
        Assert.Equal("solutions/acme/tables/identities.0.json", artifact.Path.Value);
        Assert.Equal(3, artifact.Payload.Length);
    }

    [Theory]
    [Trait("Requirement", "PUB-02")]
    [InlineData("/manifest.json")]
    [InlineData("C:/output/manifest.json")]
    [InlineData("..\\manifest.json")]
    [InlineData("solutions/../manifest.json")]
    public void RelativeArtifactPath_RootedOrEscapingPath_IsRejected(string path)
    {
        var exception = Assert.Throws<ArgumentException>(() => new RelativeArtifactPath(path));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "PKG-01")]
    public void CommittedPackage_ReferencesDirectoryDigestAndCertification()
    {
        var committed = new CommittedPackage(
            "packages/acme",
            "digest-package",
            new PackageCertification(ImmutableArray.Create(
                new SolutionCertification(
                    new SolutionId("sol_0123456789abcdef"),
                    ImmutableArray.Create(new JourneyCertification(JourneyKind.Locate, JourneyCertificationStatus.Passed, "complete"))))));

        Assert.Equal("packages/acme", committed.PackageDirectory);
        Assert.Equal("digest-package", committed.PackageDigest);
        Assert.Equal(JourneyCertificationStatus.Passed, Assert.Single(Assert.Single(committed.Certification.Solutions).Journeys).Status);
    }

    [Fact]
    [Trait("Requirement", "PKG-01")]
    public void PackageManifest_RejectsTokenEstimatorOtherThanDeclaredFormula()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new PackageManifest(
                "other.estimator",
                4.0,
                includeTests: false,
                ImmutableArray.Create(Solution())));
        Assert.Equal("tokenEstimator", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "PKG-01")]
    public void PackagePlan_DefaultArtifacts_AreOwnedEmptyThenRejectedForMissingManifest()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new PackagePlan(Manifest(false), default, "digest", Measurements()));
        Assert.Equal("artifacts", exception.ParamName);
    }

    private static PackagePlan Plan(ImmutableArray<PlannedArtifact> artifacts) =>
        new(Manifest(false), artifacts, "digest-package", Measurements());

    private static PackageManifest Manifest(bool includeTests) =>
        new(
            PackageManifest.TokenEstimatorName,
            PackageManifest.TokenDivisorValue,
            includeTests,
            ImmutableArray.Create(Solution()));

    private static SolutionManifestEntry Solution() =>
        new(
            new SolutionId("sol_0123456789abcdef"),
            "src/Acme.sln",
            Root(),
            Indexes(),
            Journeys());

    private static RootsManifestEntry Root() => new("indexes/roots.json", 1);

    private static ImmutableArray<IndexManifestEntry> Indexes() =>
        Enum.GetValues<NavigationIndexKind>()
            .Select(kind => new IndexManifestEntry(kind, $"indexes/{Snake(kind)}.json"))
            .ToImmutableArray();

    private static ImmutableArray<JourneyManifestEntry> Journeys() =>
        [
            new(JourneyKind.Locate, NavigationIndexKind.Roots),
            new(JourneyKind.FollowFlow, NavigationIndexKind.Outgoing),
            new(JourneyKind.ReverseImpact, NavigationIndexKind.Incoming),
            new(JourneyKind.EvidenceDisposition, NavigationIndexKind.Evidence),
        ];

    private static string Snake(NavigationIndexKind kind) => kind switch
    {
        NavigationIndexKind.Identity => "identity",
        NavigationIndexKind.Roots => "roots",
        NavigationIndexKind.Outgoing => "outgoing",
        NavigationIndexKind.Incoming => "incoming",
        NavigationIndexKind.Contracts => "contracts",
        NavigationIndexKind.Persistence => "persistence",
        NavigationIndexKind.Evidence => "evidence",
        NavigationIndexKind.Measures => "measures",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static PlannedArtifact ManifestArtifact() =>
        new(new RelativeArtifactPath("manifest.json"), ArtifactFamily.Manifest, ImmutableArray.Create<byte>(1, 2), 1, "digest-manifest");

    private static PlannedArtifact TableArtifact() =>
        new(
            new RelativeArtifactPath("solutions/acme/tables/identities.0.json"),
            ArtifactFamily.Table,
            ImmutableArray.Create<byte>(3, 4, 5),
            2,
            "digest-table");

    private static PlannedArtifact DeferredPayloadArtifact() =>
        new(new RelativeArtifactPath("pending.json"), ArtifactFamily.Manifest, default, 1, "digest-pending");

    private static ImmutableArray<PlannedArtifact> Include(params PlannedArtifact[] artifacts) =>
        artifacts.ToImmutableArray();

    private static PublicationMeasurements Measurements() =>
        new(new ExtractionMeasurements(1, 0), publishedArtifactCount: 1, ImmutableArray<FilteredCount>.Empty);
}
