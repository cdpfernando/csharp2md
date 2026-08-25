using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class TaxonomyTablesAdditivityTests
{
    [Fact]
    [Trait("Requirement", "TAX-88")]
    public void TaxonomyTables_ExtendingACopyWithANewFactTypeKeepsExistingTriplesValidAndLeavesTheOriginalUntouched()
    {
        var original = TaxonomyTables.Default;
        var addedType = new FactTypeDescriptor(FactFamily.Structural, "TestOnlyFactType", ["owner"]);
        var extended = original with { FactTypes = original.FactTypes.Add(addedType) };

        var extendedRegistry = new TaxonomyRegistry(extended);

        Assert.True(extendedRegistry.IsRegisteredTriple(RelationKind.Contains, "Solution", "Project"));
        Assert.Contains(extended.FactTypes, type => type.Name == "TestOnlyFactType");
        Assert.DoesNotContain(original.FactTypes, type => type.Name == "TestOnlyFactType");
    }

    [Fact]
    [Trait("Requirement", "TAX-88")]
    public void TaxonomyTables_ExtendingACopyWithANewTripleKeepsExistingTriplesValidAndLeavesTheOriginalUntouched()
    {
        var original = TaxonomyTables.Default;
        var containsDescriptor = original.Relations.Single(relation => relation.Kind == RelationKind.Contains);
        var extendedContains = containsDescriptor with { Triples = containsDescriptor.Triples.Add(new RelationTriple("Project", "Symbol")) };
        var extended = original with { Relations = original.Relations.Replace(containsDescriptor, extendedContains) };

        var extendedRegistry = new TaxonomyRegistry(extended);
        var originalRegistry = new TaxonomyRegistry(original);

        Assert.True(extendedRegistry.IsRegisteredTriple(RelationKind.Contains, "Solution", "Project"));
        Assert.True(extendedRegistry.IsRegisteredTriple(RelationKind.Contains, "Project", "Symbol"));
        Assert.False(originalRegistry.IsRegisteredTriple(RelationKind.Contains, "Project", "Symbol"));
    }

    [Fact]
    [Trait("Requirement", "TAX-89")]
    public void TaxonomyTables_RemovingARegisteredTripleFromACopyIsDetectedRatherThanSilentlyAccepted()
    {
        var original = TaxonomyTables.Default;
        var containsDescriptor = original.Relations.Single(relation => relation.Kind == RelationKind.Contains);
        var narrowedContains = containsDescriptor with { Triples = containsDescriptor.Triples.Remove(new RelationTriple("Document", "Symbol")) };
        var narrowed = original with { Relations = original.Relations.Replace(containsDescriptor, narrowedContains) };

        var originalRegistry = new TaxonomyRegistry(original);
        var narrowedRegistry = new TaxonomyRegistry(narrowed);

        Assert.True(originalRegistry.IsRegisteredTriple(RelationKind.Contains, "Document", "Symbol"));
        Assert.False(narrowedRegistry.IsRegisteredTriple(RelationKind.Contains, "Document", "Symbol"));
    }

    [Fact]
    [Trait("Requirement", "TAX-89")]
    public void TaxonomyTables_RemovingAClosedAxisValueFromACopyIsDetectedRatherThanSilentlyAccepted()
    {
        var original = TaxonomyTables.Default;
        var protocolAxis = original.FacetAxes.Single(axis => axis.Name == "boundary-protocol");
        var narrowedAxis = protocolAxis with { Values = protocolAxis.Values.Remove("http") };
        var narrowed = original with { FacetAxes = original.FacetAxes.Replace(protocolAxis, narrowedAxis) };

        Assert.Contains("http", original.FacetAxes.Single(axis => axis.Name == "boundary-protocol").Values);
        Assert.DoesNotContain("http", narrowed.FacetAxes.Single(axis => axis.Name == "boundary-protocol").Values);
    }

    [Fact]
    [Trait("Requirement", "TAX-89")]
    public void TaxonomyTables_RenamingAClosedAxisValueFromACopyIsDetectedRatherThanSilentlyAccepted()
    {
        var original = TaxonomyTables.Default;
        var protocolAxis = original.FacetAxes.Single(axis => axis.Name == "boundary-protocol");
        var renamedAxis = protocolAxis with { Values = protocolAxis.Values.Replace("http", "https-only") };
        var renamed = original with { FacetAxes = original.FacetAxes.Replace(protocolAxis, renamedAxis) };

        Assert.Contains("http", original.FacetAxes.Single(axis => axis.Name == "boundary-protocol").Values);
        Assert.DoesNotContain("http", renamed.FacetAxes.Single(axis => axis.Name == "boundary-protocol").Values);
        Assert.Contains("https-only", renamed.FacetAxes.Single(axis => axis.Name == "boundary-protocol").Values);
    }
}
