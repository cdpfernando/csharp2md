using Csharp2Md.Core.Analysis;

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

    public ImmutableArray<RootManifestEntry> Roots { get; }

    public ImmutableArray<IndexManifestEntry> Indexes { get; }

    public ImmutableArray<JourneyManifestEntry> Journeys { get; }

    public PackageManifest(
        string tokenEstimator,
        double tokenDivisor,
        bool includeTests,
        ImmutableArray<SolutionManifestEntry> solutions,
        ImmutableArray<RootManifestEntry> roots,
        ImmutableArray<IndexManifestEntry> indexes,
        ImmutableArray<JourneyManifestEntry> journeys)
    {
        if (tokenEstimator != TokenEstimatorName)
        {
            throw new ArgumentException("The manifest must declare csharp2md.tokens.bytes-per-token-v1.", nameof(tokenEstimator));
        }

        if (tokenDivisor != TokenDivisorValue)
        {
            throw new ArgumentException("The token divisor must be 4.0.", nameof(tokenDivisor));
        }

        var ownedJourneys = journeys.IsDefault ? ImmutableArray<JourneyManifestEntry>.Empty : ImmutableArray.CreateRange(journeys);
        if (ownedJourneys.Length != 4)
        {
            throw new ArgumentException("The manifest must declare the four supported journeys.", nameof(journeys));
        }

        TokenEstimator = tokenEstimator;
        TokenDivisor = tokenDivisor;
        IncludeTests = includeTests;
        Solutions = solutions.IsDefault ? ImmutableArray<SolutionManifestEntry>.Empty : ImmutableArray.CreateRange(solutions);
        Roots = roots.IsDefault ? ImmutableArray<RootManifestEntry>.Empty : ImmutableArray.CreateRange(roots);
        Indexes = indexes.IsDefault ? ImmutableArray<IndexManifestEntry>.Empty : ImmutableArray.CreateRange(indexes);
        Journeys = ownedJourneys;
    }

}

internal sealed record PackageGenerationPointer
{
    public string Generation { get; }

    public PackageGenerationPointer(string generation) => Generation = CanonicalText.Require(generation, nameof(generation));
}

internal sealed record SolutionManifestEntry
{
    public string LogicalRelativePath { get; }

    public SolutionManifestEntry(string logicalRelativePath)
    {
        LogicalRelativePath = LogicalPath.RequireRelative(logicalRelativePath, nameof(logicalRelativePath));
    }
}

internal sealed record RootManifestEntry
{
    public string DisplayName { get; }

    public string Handle { get; }

    public string MachineCitation { get; }

    public string MarkdownPath { get; }

    public RootManifestEntry(string displayName, string handle, string machineCitation, string markdownPath)
    {
        DisplayName = CanonicalText.Require(displayName, nameof(displayName));
        Handle = CanonicalText.Require(handle, nameof(handle));
        MachineCitation = CanonicalText.Require(machineCitation, nameof(machineCitation));
        MarkdownPath = LogicalPath.RequireRelative(markdownPath, nameof(markdownPath));
    }
}

internal sealed record IndexManifestEntry
{
    public string Name { get; }

    public string Path { get; }

    public IndexManifestEntry(string name, string path)
    {
        Name = CanonicalText.Require(name, nameof(name));
        Path = LogicalPath.RequireRelative(path, nameof(path));
    }
}

internal sealed record JourneyManifestEntry
{
    public JourneyKind Kind { get; }

    public string EntryPath { get; }

    public JourneyManifestEntry(JourneyKind kind, string entryPath)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        EntryPath = LogicalPath.RequireRelative(entryPath, nameof(entryPath));
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
    public ImmutableArray<JourneyCertification> Journeys { get; }

    public PackageCertification(ImmutableArray<JourneyCertification> journeys)
    {
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

internal enum JourneyKind
{
    Locate,
    FollowFlow,
    ReverseImpact,
    EvidenceDisposition,
}

internal enum JourneyCertificationStatus
{
    Passed,
    Failed,
    NotApplicable,
}
