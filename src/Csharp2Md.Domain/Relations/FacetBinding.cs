using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Relations;

public readonly record struct FacetBindingEntry(string AxisName, string WireValue);

public readonly struct FacetBinding : IEquatable<FacetBinding>
{
    public ImmutableArray<FacetBindingEntry> Entries { get; }

    private FacetBinding(ImmutableArray<FacetBindingEntry> entries) => Entries = entries;

    public static FacetBinding Create(
        ImmutableArray<FacetAxisDescriptor> registeredAxes,
        ImmutableArray<string> allowedAxisNames,
        IEnumerable<FacetBindingEntry> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (registeredAxes.IsDefault)
        {
            throw new ArgumentException("A facet binding requires the registered axis descriptors.", nameof(registeredAxes));
        }

        if (allowedAxisNames.IsDefault)
        {
            throw new ArgumentException("A facet binding requires the set of axes relevant to its relation.", nameof(allowedAxisNames));
        }

        var axesByName = registeredAxes.ToDictionary(static axis => axis.Name, StringComparer.Ordinal);

        var ordered = values
            .Select(entry => RequireRegistered(entry, axesByName, allowedAxisNames, nameof(values)))
            .Distinct()
            .OrderBy(static entry => entry.AxisName, StringComparer.Ordinal)
            .ThenBy(static entry => entry.WireValue, StringComparer.Ordinal)
            .ToImmutableArray();

        return new FacetBinding(ordered);
    }

    public bool Equals(FacetBinding other) => Entries.AsSpan().SequenceEqual(other.Entries.AsSpan());

    public override bool Equals(object? obj) => obj is FacetBinding other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(FacetBinding left, FacetBinding right) => left.Equals(right);

    public static bool operator !=(FacetBinding left, FacetBinding right) => !left.Equals(right);

    private static FacetBindingEntry RequireRegistered(
        FacetBindingEntry entry,
        IReadOnlyDictionary<string, FacetAxisDescriptor> axesByName,
        ImmutableArray<string> allowedAxisNames,
        string parameterName)
    {
        if (!allowedAxisNames.Contains(entry.AxisName, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Axis '{entry.AxisName}' is not relevant to this relation and cannot be bound.", parameterName);
        }

        if (!axesByName.TryGetValue(entry.AxisName, out var axis) || !axis.Values.Contains(entry.WireValue, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"'{entry.WireValue}' is not a registered value of the '{entry.AxisName}' axis.", parameterName);
        }

        return entry;
    }
}
