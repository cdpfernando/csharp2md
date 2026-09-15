using Csharp2Md.Core.Analysis;
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
        Assert.Equal("src/Acme.sln", Assert.Single(manifest.Solutions).LogicalRelativePath);
        var root = Assert.Single(manifest.Roots);
        Assert.Equal("Orders", root.DisplayName);
        Assert.Equal("0", root.Handle);
        Assert.Equal("indexes/roots.json#0", root.MachineCitation);
        Assert.Equal("markdown/components/0.md", root.MarkdownPath);
        Assert.Equal("indexes/identity.json", Assert.Single(manifest.Indexes).Path);
        Assert.Equal(4, manifest.Journeys.Length);
        Assert.Equal(
            [JourneyKind.Locate, JourneyKind.FollowFlow, JourneyKind.ReverseImpact, JourneyKind.EvidenceDisposition],
            manifest.Journeys.Select(journey => journey.Kind).ToArray());
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
                new JourneyCertification(JourneyKind.Locate, JourneyCertificationStatus.Passed, "complete"))));

        Assert.Equal("packages/acme", committed.PackageDirectory);
        Assert.Equal("digest-package", committed.PackageDigest);
        Assert.Equal(JourneyCertificationStatus.Passed, Assert.Single(committed.Certification.Journeys).Status);
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
                ImmutableArray.Create(new SolutionManifestEntry("src/Acme.sln")),
                ImmutableArray.Create(Root()),
                ImmutableArray.Create(new IndexManifestEntry("identity", "indexes/identity.json")),
                Journeys()));
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
            ImmutableArray.Create(new SolutionManifestEntry("src/Acme.sln")),
            ImmutableArray.Create(Root()),
            ImmutableArray.Create(new IndexManifestEntry("identity", "indexes/identity.json")),
            Journeys());

    private static RootManifestEntry Root() =>
        new("Orders", "0", "indexes/roots.json#0", "markdown/components/0.md");

    private static ImmutableArray<JourneyManifestEntry> Journeys() =>
        [
            new(JourneyKind.Locate, "indexes/roots.json"),
            new(JourneyKind.FollowFlow, "indexes/outgoing.json"),
            new(JourneyKind.ReverseImpact, "indexes/incoming.json"),
            new(JourneyKind.EvidenceDisposition, "indexes/evidence.json"),
        ];

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
