using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding.Identity;
using System.Text.Json.Serialization;

namespace Csharp2Md.Core.Publication;

internal sealed record PackagePlan
{
    public PackageManifest Manifest { get; }

    public ImmutableArray<PlannedArtifact> Artifacts { get; }

    public string PackageDigest { get; }

    public PublicationMeasurements Measurements { get; }

    public PackagePlan(
        PackageManifest manifest,
        ImmutableArray<PlannedArtifact> artifacts,
        string packageDigest,
        PublicationMeasurements measurements)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(measurements);
        var owned = artifacts.IsDefault ? ImmutableArray<PlannedArtifact>.Empty : ImmutableArray.CreateRange(artifacts);
        if (owned.IsDefaultOrEmpty || owned.All(static artifact => artifact.Path.Value != "manifest.json"))
        {
            throw new ArgumentException("A package plan must contain the root manifest payload before staging.", nameof(artifacts));
        }

        if (owned.Any(static artifact => artifact.Payload.IsDefaultOrEmpty))
        {
            throw new ArgumentException("A package plan cannot contain deferred or empty payloads.", nameof(artifacts));
        }

        Manifest = manifest;
        Artifacts = owned;
        PackageDigest = CanonicalText.Require(packageDigest, nameof(packageDigest));
        Measurements = measurements;
    }
}

internal sealed record PlannedArtifact
{
    public RelativeArtifactPath Path { get; }

    public ArtifactFamily Family { get; }

    public ImmutableArray<byte> Payload { get; }

    public int RecordCount { get; }

    public string ContentDigest { get; }

    public PlannedArtifact(
        RelativeArtifactPath path,
        ArtifactFamily family,
        ImmutableArray<byte> payload,
        int recordCount,
        string contentDigest)
    {
        if (!Enum.IsDefined(family))
        {
            throw new ArgumentOutOfRangeException(nameof(family));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(recordCount);
        Path = path;
        Family = family;
        Payload = payload.IsDefault ? ImmutableArray<byte>.Empty : ImmutableArray.CreateRange(payload);
        RecordCount = recordCount;
        ContentDigest = CanonicalText.Require(contentDigest, nameof(contentDigest));
    }
}

internal sealed record PackageManifest
{
    public const string TokenEstimatorName = "csharp2md.tokens.bytes-per-token-v1";
    public const double TokenDivisorValue = 4.0;

    public string TokenEstimator { get; }

    public double TokenDivisor { get; }

    public bool IncludeTests { get; }

    public ImmutableArray<SolutionManifestEntry> Solutions { get; }

    public PackageManifest(
        string tokenEstimator,
        double tokenDivisor,
        bool includeTests,
        ImmutableArray<SolutionManifestEntry> solutions)
    {
        if (tokenEstimator != TokenEstimatorName)
        {
            throw new ArgumentException("The manifest must declare csharp2md.tokens.bytes-per-token-v1.", nameof(tokenEstimator));
        }

        if (tokenDivisor != TokenDivisorValue)
        {
            throw new ArgumentException("The token divisor must be 4.0.", nameof(tokenDivisor));
        }

        var ownedSolutions = solutions.IsDefault ? ImmutableArray<SolutionManifestEntry>.Empty : ImmutableArray.CreateRange(solutions);
        if (ownedSolutions.Select(solution => solution.Id).Distinct().Count() != ownedSolutions.Length)
        {
            throw new ArgumentException("The manifest cannot declare the same solution ID more than once.", nameof(solutions));
        }

        TokenEstimator = tokenEstimator;
        TokenDivisor = tokenDivisor;
        IncludeTests = includeTests;
        Solutions = ownedSolutions;
    }

}

internal sealed record PackageGenerationPointer
{
    public string Generation { get; }

    public PackageGenerationPointer(string generation) => Generation = CanonicalText.Require(generation, nameof(generation));
}

internal sealed record SolutionManifestEntry
{
    public SolutionId Id { get; }

    public string LogicalRelativePath { get; }

    public RootsManifestEntry Roots { get; }

    public ImmutableArray<IndexManifestEntry> Indexes { get; }

    public ImmutableArray<JourneyManifestEntry> Journeys { get; }

    public SolutionManifestEntry(
        SolutionId id,
        string logicalRelativePath,
        RootsManifestEntry roots,
        ImmutableArray<IndexManifestEntry> indexes,
        ImmutableArray<JourneyManifestEntry> journeys)
    {
        var ownedIndexes = indexes.IsDefault ? ImmutableArray<IndexManifestEntry>.Empty : ImmutableArray.CreateRange(indexes);
        var expectedIndexes = Enum.GetValues<NavigationIndexKind>();
        if (ownedIndexes.Length != expectedIndexes.Length || ownedIndexes.Select(index => index.Kind).Distinct().Count() != expectedIndexes.Length)
        {
            throw new ArgumentException("A solution manifest must declare exactly one index of every supported kind.", nameof(indexes));
        }

        var ownedJourneys = journeys.IsDefault ? ImmutableArray<JourneyManifestEntry>.Empty : ImmutableArray.CreateRange(journeys);
        var expectedJourneys = Enum.GetValues<JourneyKind>();
        if (ownedJourneys.Length != expectedJourneys.Length || ownedJourneys.Select(journey => journey.Kind).Distinct().Count() != expectedJourneys.Length)
        {
            throw new ArgumentException("A solution manifest must declare exactly one entry for every supported journey.", nameof(journeys));
        }

        if (ownedJourneys.Any(journey => ownedIndexes.All(index => index.Kind != journey.EntryIndex)))
        {
            throw new ArgumentException("Every journey entry must reference a declared index.", nameof(journeys));
        }

        Id = id;
        LogicalRelativePath = LogicalPath.RequireRelative(logicalRelativePath, nameof(logicalRelativePath));
        Roots = roots ?? throw new ArgumentNullException(nameof(roots));
        Indexes = ownedIndexes;
        Journeys = ownedJourneys;
    }
}

internal sealed record RootsManifestEntry
{
    public string EntryPath { get; }

    public int Count { get; }

    public RootsManifestEntry(string entryPath, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        EntryPath = LogicalPath.RequireRelative(entryPath, nameof(entryPath));
        Count = count;
    }
}

internal sealed record IndexManifestEntry
{
    public NavigationIndexKind Kind { get; }

    public string EntryPath { get; }

    public IndexManifestEntry(NavigationIndexKind kind, string entryPath)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        EntryPath = LogicalPath.RequireRelative(entryPath, nameof(entryPath));
    }
}

internal sealed record JourneyManifestEntry
{
    public JourneyKind Kind { get; }

    public NavigationIndexKind EntryIndex { get; }

    public JourneyManifestEntry(JourneyKind kind, NavigationIndexKind entryIndex)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(entryIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(entryIndex));
        }

        Kind = kind;
        EntryIndex = entryIndex;
    }
}

internal sealed record PublicationMeasurements
{
    public ExtractionMeasurements Extraction { get; }

    public int PublishedArtifactCount { get; }

    public ImmutableArray<FilteredCount> FilteredByReason { get; }

    public PublicationMeasurements(
        ExtractionMeasurements extraction,
        int publishedArtifactCount,
        ImmutableArray<FilteredCount> filteredByReason)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentOutOfRangeException.ThrowIfNegative(publishedArtifactCount);
        Extraction = extraction;
        PublishedArtifactCount = publishedArtifactCount;
        FilteredByReason = filteredByReason.IsDefault
            ? ImmutableArray<FilteredCount>.Empty
            : ImmutableArray.CreateRange(filteredByReason);
    }
}

internal sealed record FilteredCount
{
    public string Reason { get; }

    public int Count { get; }

    public FilteredCount(string reason, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Reason = CanonicalText.Require(reason, nameof(reason));
        Count = count;
    }
}

internal sealed record CommittedPackage
{
    public string PackageDirectory { get; }

    public string PackageDigest { get; }

    public PackageCertification Certification { get; }

    public CommittedPackage(string packageDirectory, string packageDigest, PackageCertification certification)
    {
        ArgumentNullException.ThrowIfNull(certification);
        PackageDirectory = CanonicalText.Require(packageDirectory, nameof(packageDirectory));
        PackageDigest = CanonicalText.Require(packageDigest, nameof(packageDigest));
        Certification = certification;
    }
}

internal sealed record PackageCertification
{
    public ImmutableArray<SolutionCertification> Solutions { get; }

    public PackageCertification(ImmutableArray<SolutionCertification> solutions)
    {
        Solutions = solutions.IsDefault
            ? ImmutableArray<SolutionCertification>.Empty
            : ImmutableArray.CreateRange(solutions);
    }
}

internal sealed record SolutionCertification
{
    public SolutionId SolutionId { get; }

    public ImmutableArray<JourneyCertification> Journeys { get; }

    public SolutionCertification(SolutionId solutionId, ImmutableArray<JourneyCertification> journeys)
    {
        SolutionId = solutionId;
        Journeys = journeys.IsDefault
            ? ImmutableArray<JourneyCertification>.Empty
            : ImmutableArray.CreateRange(journeys);
    }
}

internal sealed record JourneyCertification
{
    public JourneyKind Kind { get; }

    public JourneyCertificationStatus Status { get; }

    public string Detail { get; }

    public JourneyCertification(JourneyKind kind, JourneyCertificationStatus status, string detail)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Kind = kind;
        Status = status;
        Detail = CanonicalText.Require(detail, nameof(detail));
    }
}

internal readonly record struct RelativeArtifactPath
{
    public string Value { get; }

    public RelativeArtifactPath(string value) =>
        Value = LogicalPath.RequireRelative(value, nameof(value));
}

internal enum ArtifactFamily
{
    Manifest,
    Table,
    Graph,
    Index,
    Measure,
    Markdown,
    Source,
    Certification,
    Measurement,
}

[JsonConverter(typeof(JsonStringEnumConverter<NavigationIndexKind>))]
internal enum NavigationIndexKind
{
    [JsonStringEnumMemberName("identity")] Identity,
    [JsonStringEnumMemberName("roots")] Roots,
    [JsonStringEnumMemberName("outgoing")] Outgoing,
    [JsonStringEnumMemberName("incoming")] Incoming,
    [JsonStringEnumMemberName("contracts")] Contracts,
    [JsonStringEnumMemberName("persistence")] Persistence,
    [JsonStringEnumMemberName("evidence")] Evidence,
    [JsonStringEnumMemberName("measures")] Measures,
}

[JsonConverter(typeof(JsonStringEnumConverter<JourneyKind>))]
internal enum JourneyKind
{
    [JsonStringEnumMemberName("locate")] Locate,
    [JsonStringEnumMemberName("follow_flow")] FollowFlow,
    [JsonStringEnumMemberName("reverse_impact")] ReverseImpact,
    [JsonStringEnumMemberName("evidence_disposition")] EvidenceDisposition,
}

internal enum JourneyCertificationStatus
{
    Passed,
    Failed,
    NotApplicable,
}
