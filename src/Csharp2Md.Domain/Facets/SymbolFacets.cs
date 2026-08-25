namespace Csharp2Md.Domain.Facets;

public enum SymbolFacet
{
    Callable,
    Controller,
    Handler,
    Repository,
    Client,
    Service,
}

public readonly struct SymbolFacetSet : IEquatable<SymbolFacetSet>
{
    public ImmutableArray<SymbolFacet> Facets { get; }

    private SymbolFacetSet(ImmutableArray<SymbolFacet> facets) => Facets = facets;

    public static SymbolFacetSet Create(IEnumerable<SymbolFacet> facets)
    {
        ArgumentNullException.ThrowIfNull(facets);

        var ordered = facets
            .Select(facet => RequireDefined(facet, nameof(facets)))
            .Distinct()
            .Order()
            .ToImmutableArray();

        return new SymbolFacetSet(ordered);
    }

    public bool Equals(SymbolFacetSet other) => Facets.AsSpan().SequenceEqual(other.Facets.AsSpan());

    public override bool Equals(object? obj) => obj is SymbolFacetSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var facet in Facets)
        {
            hash.Add(facet);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(SymbolFacetSet left, SymbolFacetSet right) => left.Equals(right);

    public static bool operator !=(SymbolFacetSet left, SymbolFacetSet right) => !left.Equals(right);

    private static SymbolFacet RequireDefined(SymbolFacet facet, string parameterName)
    {
        if (!Enum.IsDefined(facet))
        {
            throw new ArgumentOutOfRangeException(parameterName, facet, $"'{facet}' is not a defined value of the '{nameof(SymbolFacet)}' axis.");
        }

        return facet;
    }
}
