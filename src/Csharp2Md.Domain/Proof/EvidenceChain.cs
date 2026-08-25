using System.Globalization;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Domain.Proof;

public readonly struct EvidenceChain : IEquatable<EvidenceChain>
{
    public ImmutableArray<ObservationIdentity> DerivedFrom { get; }

    private EvidenceChain(ImmutableArray<ObservationIdentity> derivedFrom) => DerivedFrom = derivedFrom;

    public static EvidenceChain Create(IEnumerable<ObservationIdentity> derivedFrom)
    {
        ArgumentNullException.ThrowIfNull(derivedFrom);

        var ordered = derivedFrom
            .Distinct()
            .OrderBy(SortKey, StringComparer.Ordinal)
            .ToImmutableArray();

        if (ordered.IsEmpty)
        {
            throw new ArgumentException("An evidence chain must derive from at least one observation.", nameof(derivedFrom));
        }

        return new EvidenceChain(ordered);
    }

    public bool Equals(EvidenceChain other) => DerivedFrom.AsSpan().SequenceEqual(other.DerivedFrom.AsSpan());

    public override bool Equals(object? obj) => obj is EvidenceChain other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var identity in DerivedFrom)
        {
            hash.Add(identity);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(EvidenceChain left, EvidenceChain right) => left.Equals(right);

    public static bool operator !=(EvidenceChain left, EvidenceChain right) => !left.Equals(right);

    private static string SortKey(ObservationIdentity identity) =>
        string.Join(
            '\u0000',
            identity.Owner.Id.Value,
            identity.Kind.ToString(),
            identity.OccurrenceOrdinal.ToString("D10", CultureInfo.InvariantCulture));
}
