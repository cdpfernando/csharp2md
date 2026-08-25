using Csharp2Md.Domain.Facets;

namespace Csharp2Md.Domain.Tests.Facets;

public sealed class SymbolFacetsTests
{
    [Fact]
    [Trait("Requirement", "TAX-14")]
    public void SymbolFacet_HasCallableAsAValueAndNoCallableFactTypeExists()
    {
        Assert.True(Enum.IsDefined(SymbolFacet.Callable));

        var callableType = typeof(SymbolFacet).Assembly.GetTypes()
            .FirstOrDefault(type => type.Name == "Callable");

        Assert.True(callableType is null, "No type named 'Callable' may exist anywhere in the domain.");
    }

    [Fact]
    [Trait("Requirement", "TAX-13")]
    public void SymbolFacet_HasControllerHandlerRepositoryClientAndServiceAsValues()
    {
        var names = Enum.GetNames<SymbolFacet>();

        Assert.Contains("Controller", names);
        Assert.Contains("Handler", names);
        Assert.Contains("Repository", names);
        Assert.Contains("Client", names);
        Assert.Contains("Service", names);
    }

    [Fact]
    [Trait("Requirement", "TAX-13")]
    public void SymbolFacet_IsNotAFlagsEnum() =>
        Assert.Empty(typeof(SymbolFacet).GetCustomAttributes(typeof(FlagsAttribute), inherit: false));

    [Fact]
    [Trait("Requirement", "TAX-13")]
    public void Create_TwoOrdersWithADuplicate_ProduceOneEqualOrderedSet()
    {
        var forward = SymbolFacetSet.Create([SymbolFacet.Service, SymbolFacet.Controller, SymbolFacet.Service]);
        var reverse = SymbolFacetSet.Create([SymbolFacet.Controller, SymbolFacet.Service]);

        Assert.Equal(forward, reverse);
        Assert.Equal(new[] { SymbolFacet.Controller, SymbolFacet.Service }, reverse.Facets.ToArray());
    }

    [Fact]
    [Trait("Requirement", "TAX-13")]
    public void Create_UndefinedFacetReachedByCast_IsRejectedNamingTheAxisAndTheValue()
    {
        var undefined = (SymbolFacet)99;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => SymbolFacetSet.Create([undefined]));

        Assert.Contains(nameof(SymbolFacet), exception.Message, StringComparison.Ordinal);
        Assert.Equal(undefined, exception.ActualValue);
    }
}
