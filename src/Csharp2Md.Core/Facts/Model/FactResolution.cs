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
