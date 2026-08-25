using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Domain.Relations;

public enum FrontierCause
{
    FurtherContinuationObserved,
}

public sealed record OpenFrontier
{
    public ObservationIdentity Occurrence { get; }

    public FrontierCause Cause { get; }

    public Frontier Frontier => Frontier.Open;

    private OpenFrontier(ObservationIdentity occurrence, FrontierCause cause)
    {
        Occurrence = occurrence;
        Cause = cause;
    }

    public static OpenFrontier Create(ObservationIdentity occurrence, FrontierCause? cause)
    {
        if (cause is null)
        {
            throw new ArgumentNullException(nameof(cause), "An open frontier must carry its cause.");
        }

        if (!Enum.IsDefined(cause.Value))
        {
            throw new ArgumentException($"'{cause}' is not a defined value of the '{nameof(FrontierCause)}' axis.", nameof(cause));
        }

        return new OpenFrontier(occurrence, cause.Value);
    }
}
