using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Facts.Metadata;

public readonly record struct FactProvenance : IComparable<FactProvenance>
{
    public string EngineId { get; }

    public string EngineVersion { get; }

    public DetectorId? DetectorId { get; }

    public string? DetectorVersion { get; }

    public FactProvenance(
        string engineId,
        string engineVersion,
        DetectorId? detectorId = null,
        string? detectorVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineId);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        if ((detectorId is null) != (detectorVersion is null))
        {
            throw new ArgumentException("Detector identity and version must either both be present or both be absent.", nameof(detectorVersion));
        }

        if (detectorVersion is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(detectorVersion);
        }

        EngineId = engineId;
        EngineVersion = engineVersion;
        DetectorId = detectorId;
        DetectorVersion = detectorVersion;
    }

    public int CompareTo(FactProvenance other)
    {
        var engineComparison = Compare(EngineId, other.EngineId, EngineVersion, other.EngineVersion);
        return engineComparison != 0
            ? engineComparison
            : Compare(DetectorId?.Value, other.DetectorId?.Value, DetectorVersion, other.DetectorVersion);
    }

    private static int Compare(string? leftFirst, string? rightFirst, string? leftSecond, string? rightSecond)
    {
        var firstComparison = StringComparer.Ordinal.Compare(leftFirst, rightFirst);
        return firstComparison != 0
            ? firstComparison
            : StringComparer.Ordinal.Compare(leftSecond, rightSecond);
    }
}
