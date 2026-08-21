namespace Csharp2Md.Core.Facts.Model;

public enum FactResolution
{
    Exact,
    Partial,
    Syntactic,
    Heuristic,
    Candidate,
    Unresolved,
    NotApplicable,
}

public enum ConfigurationResolution
{
    HardCoded,
    Dynamic,
    Unresolved,
    NotApplicable,
}

public static class FactResolutionAlgebra
{
    /// <summary>
    /// The stronger of two resolutions for the same subject, ranked most-proven first. Used wherever
    /// several contributions describe one fact and only one resolution can survive.
    /// </summary>
    public static FactResolution Stronger(FactResolution left, FactResolution right) =>
        Rank(left) <= Rank(right) ? left : right;

    private static int Rank(FactResolution resolution) => resolution switch
    {
        FactResolution.Exact => 0,
        FactResolution.Partial => 1,
        FactResolution.Syntactic => 2,
        FactResolution.Heuristic => 3,
        FactResolution.Candidate => 4,
        FactResolution.Unresolved => 5,
        FactResolution.NotApplicable => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unknown fact resolution."),
    };

    public static FactResolution AggregateDocument(IEnumerable<FactResolution> factResolutions)
    {
        ArgumentNullException.ThrowIfNull(factResolutions);

        FactResolution? aggregate = null;
        foreach (var resolution in factResolutions)
        {
            if (!Enum.IsDefined(resolution))
            {
                throw new ArgumentOutOfRangeException(nameof(factResolutions), resolution, "Unknown fact resolution.");
            }

            if (resolution is FactResolution.NotApplicable)
            {
                continue;
            }

            if (aggregate is null)
            {
                aggregate = resolution;
                continue;
            }

            if (aggregate != resolution)
            {
                return FactResolution.Partial;
            }
        }

        return aggregate ?? FactResolution.NotApplicable;
    }
}
