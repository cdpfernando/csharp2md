using Csharp2Md.Domain.Facets;

namespace Csharp2Md.Domain.Registry;

public sealed record FacetAxisDescriptor(string Name, ImmutableArray<string> Values);

internal static class FacetAxisTable
{
    public static readonly ImmutableArray<FacetAxisDescriptor> All =
    [
        new("boundary-protocol", [.. Enum.GetValues<BoundaryProtocol>().Select(FacetAxes.WireValue)]),
        new("boundary-direction", [.. Enum.GetValues<BoundaryDirection>().Select(FacetAxes.WireValue)]),
        new("boundary-role", [.. Enum.GetValues<BoundaryRole>().Select(FacetAxes.WireValue)]),
        new("data-store-technology", [.. Enum.GetValues<DataStoreTechnology>().Select(FacetAxes.WireValue)]),
        new("data-object-form", [.. Enum.GetValues<DataObjectForm>().Select(FacetAxes.WireValue)]),
        new("data-operation-kind", [.. Enum.GetValues<DataOperationKind>().Select(FacetAxes.WireValue)]),
        new("symbol-facet", [.. Enum.GetValues<SymbolFacet>().Select(FacetAxes.WireValue)]),
        new("mapping-state-kind", [.. Enum.GetValues<MappingStateKind>().Select(FacetAxes.WireValue)]),
    ];
}

internal static class ProofAxisTable
{
    public static readonly ImmutableArray<FacetAxisDescriptor> All =
    [
        new("evidence-method", ["semantic", "syntactic", "configured"]),
        new("resolution", ["confirmed", "candidate", "unresolved"]),
        new("frontier", ["closed", "open"]),
    ];
}

public sealed partial record TaxonomyTables
{
    public ImmutableArray<FacetAxisDescriptor> FacetAxes { get; init; } = [];

    public ImmutableArray<FacetAxisDescriptor> ProofAxes { get; init; } = [];
}
