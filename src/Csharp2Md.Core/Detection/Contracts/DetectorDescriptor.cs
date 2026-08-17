using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Detection.Contracts;

internal enum DetectorLevel
{
    Project,
    Document,
}

internal sealed record DetectorDescriptor
{
    public DetectorId Id { get; }

    public string Version { get; }

    public ImmutableArray<DetectorLevel> SupportedLevels { get; }

    public ImmutableArray<FactKind> SupportedFactKinds { get; }

    private DetectorDescriptor(
        DetectorId id,
        string version,
        ImmutableArray<DetectorLevel> supportedLevels,
        ImmutableArray<FactKind> supportedFactKinds)
    {
        Id = id;
        Version = version;
        SupportedLevels = supportedLevels;
        SupportedFactKinds = supportedFactKinds;
    }

    public static DetectorDescriptor Create(
        DetectorId id,
        string version,
        IEnumerable<DetectorLevel> supportedLevels,
        IEnumerable<FactKind> supportedFactKinds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(supportedLevels);
        ArgumentNullException.ThrowIfNull(supportedFactKinds);

        var levels = supportedLevels.Distinct().Order().ToImmutableArray();
        var factKinds = supportedFactKinds.Distinct().Order().ToImmutableArray();
        if (levels.IsEmpty)
        {
            throw new ArgumentException("At least one detector level is required.", nameof(supportedLevels));
        }

        if (factKinds.IsEmpty)
        {
            throw new ArgumentException("At least one supported fact kind is required.", nameof(supportedFactKinds));
        }

        return new DetectorDescriptor(id, version, levels, factKinds);
    }
}
