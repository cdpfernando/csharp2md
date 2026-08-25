using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Observations;

public readonly record struct ObservationIdentity
{
    public FactReference Owner { get; }

    public ObservationKind Kind { get; }

    public NormalizedPayload Payload { get; }

    public int OccurrenceOrdinal { get; }

    public ObservationIdentity(FactReference owner, ObservationKind kind, NormalizedPayload payload, int occurrenceOrdinal)
    {
        if (occurrenceOrdinal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceOrdinal), occurrenceOrdinal, "A structural occurrence ordinal must be a positive integer.");
        }

        Owner = owner;
        Kind = kind;
        Payload = payload;
        OccurrenceOrdinal = occurrenceOrdinal;
    }
}
