using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Classification;

/// <summary>
/// Reads observation payload entries - the single owner of that decoding for every classifier pass,
/// the coverage stage and the persistence model. Multi-valued entries are a single <c>|</c>-joined literal because
/// <see cref="NormalizedPayload.Create"/> rejects duplicate keys.
/// </summary>
internal static class PayloadReader
{
    private const char MultiValueSeparator = '|';

    /// <summary>The first non-blank literal stored under <paramref name="key"/>, or null.</summary>
    public static string? Value(Observation observation, string key)
    {
        ArgumentNullException.ThrowIfNull(observation);

        foreach (var entry in observation.Identity.Payload.Entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(entry.Value.Value))
            {
                return entry.Value.Value;
            }
        }

        return null;
    }

    /// <summary>Whether an entry under <paramref name="key"/> contains <paramref name="needle"/>.</summary>
    public static bool Contains(Observation observation, string key, string needle)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(needle);

        foreach (var entry in observation.Identity.Payload.Entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal)
                && entry.Value.Value is { } value
                && value.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The <c>|</c>-separated names stored under <paramref name="key"/>, in stored order. A missing or
    /// blank entry yields an empty array rather than a single empty name.
    /// </summary>
    public static ImmutableArray<string> Multi(Observation observation, string key)
    {
        var value = Value(observation, key);
        return value is null
            ? []
            : [.. value.Split(MultiValueSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
