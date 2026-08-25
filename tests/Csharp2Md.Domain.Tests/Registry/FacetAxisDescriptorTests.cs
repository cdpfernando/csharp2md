using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Tests.Facets;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class FacetAxisDescriptorTests
{
    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void FacetAxisTable_ProjectsEveryDeclaredFacetsAxisWithItsWireValues()
    {
        AssertAxis("boundary-protocol", Enum.GetValues<BoundaryProtocol>().Select(FacetAxes.WireValue));
        AssertAxis("boundary-direction", Enum.GetValues<BoundaryDirection>().Select(FacetAxes.WireValue));
        AssertAxis("boundary-role", Enum.GetValues<BoundaryRole>().Select(FacetAxes.WireValue));
        AssertAxis("data-store-technology", Enum.GetValues<DataStoreTechnology>().Select(FacetAxes.WireValue));
        AssertAxis("data-object-form", Enum.GetValues<DataObjectForm>().Select(FacetAxes.WireValue));
        AssertAxis("data-operation-kind", Enum.GetValues<DataOperationKind>().Select(FacetAxes.WireValue));
        AssertAxis("symbol-facet", Enum.GetValues<SymbolFacet>().Select(FacetAxes.WireValue));
        AssertAxis("mapping-state-kind", Enum.GetValues<MappingStateKind>().Select(FacetAxes.WireValue));

        Assert.Equal(8, FacetAxisTable.All.Length);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void ProofAxisTable_ProjectsAllThreeProofAxesWithTheirDocumentedValues()
    {
        AssertProofAxis("evidence-method", Enum.GetNames<EvidenceMethod>());
        AssertProofAxis("resolution", Enum.GetNames<Resolution>());
        AssertProofAxis("frontier", Enum.GetNames<Frontier>());

        Assert.Equal(3, ProofAxisTable.All.Length);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void TaxonomyTables_FacetAndProofAxesAreReachableOrderedArrays()
    {
        Assert.Equal(
            typeof(ImmutableArray<FacetAxisDescriptor>),
            typeof(TaxonomyTables).GetProperty(nameof(TaxonomyTables.FacetAxes))!.PropertyType);
        Assert.Equal(
            typeof(ImmutableArray<FacetAxisDescriptor>),
            typeof(TaxonomyTables).GetProperty(nameof(TaxonomyTables.ProofAxes))!.PropertyType);
    }

    private static void AssertAxis(string axisName, IEnumerable<string> expectedValues)
    {
        var axis = FacetAxisTable.All.Single(entry => entry.Name == axisName);

        Assert.Equal(expectedValues.ToHashSet(), axis.Values.ToHashSet());
    }

    private static void AssertProofAxis(string axisName, IEnumerable<string> pascalCaseMemberNames)
    {
        var axis = ProofAxisTable.All.Single(entry => entry.Name == axisName);
        var expectedValues = pascalCaseMemberNames.Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Equal(expectedValues, axis.Values.ToHashSet());
    }
}
