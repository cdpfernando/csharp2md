using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Observations;

public readonly record struct PayloadEntry
{
    public string Key { get; }

    public StructuralLiteral Value { get; }

    public PayloadEntry(string key, StructuralLiteral value)
    {
        Key = FactIdGrammar.RequireCanonicalText(key, nameof(key));
        Value = value;
    }
}

public readonly struct NormalizedPayload : IEquatable<NormalizedPayload>
{
    public ImmutableArray<PayloadEntry> Entries { get; }

    private NormalizedPayload(ImmutableArray<PayloadEntry> entries) => Entries = entries;

    public static NormalizedPayload Create(IEnumerable<PayloadEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToImmutableArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            if (string.Equals(ordered[i].Key, ordered[i - 1].Key, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Duplicate payload key '{ordered[i].Key}'.", nameof(entries));
            }
        }

        return new NormalizedPayload(ordered);
    }

    public bool Equals(NormalizedPayload other) => Entries.AsSpan().SequenceEqual(other.Entries.AsSpan());

    public override bool Equals(object? obj) => obj is NormalizedPayload other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in Entries)
        {
            hash.Add(entry.Key, StringComparer.Ordinal);
            hash.Add(entry.Value);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(NormalizedPayload left, NormalizedPayload right) => left.Equals(right);

    public static bool operator !=(NormalizedPayload left, NormalizedPayload right) => !left.Equals(right);
}
