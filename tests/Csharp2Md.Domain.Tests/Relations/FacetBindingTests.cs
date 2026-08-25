using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class FacetBindingTests
{
    private static readonly ImmutableArray<FacetAxisDescriptor> RegisteredAxes = TaxonomyTables.Default.FacetAxes;

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_UnregisteredValueOnARegisteredAxis_IsRejectedNamingAxisAndValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => FacetBinding.Create(
            RegisteredAxes,
            ["symbol-facet"],
            [new FacetBindingEntry("symbol-facet", "not-a-registered-facet")]));

        Assert.Equal("values", exception.ParamName);
        Assert.Contains("symbol-facet", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not-a-registered-facet", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_RegisteredValue_IsAccepted()
    {
        var binding = FacetBinding.Create(
            RegisteredAxes,
            ["symbol-facet"],
            [new FacetBindingEntry("symbol-facet", "callable")]);

        Assert.Single(binding.Entries);
        Assert.Equal(new FacetBindingEntry("symbol-facet", "callable"), binding.Entries[0]);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_ShuffledInputs_ProduceEqualBindings()
    {
        FacetBindingEntry[] forwardOrder =
        [
            new("boundary-direction", "inbound"),
            new("symbol-facet", "callable"),
        ];
        FacetBindingEntry[] shuffledOrder =
        [
            new("symbol-facet", "callable"),
            new("boundary-direction", "inbound"),
        ];

        var forward = FacetBinding.Create(RegisteredAxes, ["symbol-facet", "boundary-direction"], forwardOrder);
        var shuffled = FacetBinding.Create(RegisteredAxes, ["symbol-facet", "boundary-direction"], shuffledOrder);

        Assert.Equal(forward, shuffled);
        Assert.True(forward == shuffled);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_AxisIrrelevantToTheRelationScope_CannotBeBound()
    {
        var exception = Assert.Throws<ArgumentException>(() => FacetBinding.Create(
            RegisteredAxes,
            ["symbol-facet"],
            [new FacetBindingEntry("data-object-form", "table")]));

        Assert.Equal("values", exception.ParamName);
        Assert.Contains("data-object-form", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_NullValues_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => FacetBinding.Create(RegisteredAxes, ["symbol-facet"], null!));

        Assert.Equal("values", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-60")]
    public void Create_DuplicateEntries_CollapseToOne()
    {
        var binding = FacetBinding.Create(
            RegisteredAxes,
            ["symbol-facet"],
            [new FacetBindingEntry("symbol-facet", "callable"), new FacetBindingEntry("symbol-facet", "callable")]);

        Assert.Single(binding.Entries);
    }
}
