using Csharp2Md.Domain.Facets;

namespace Csharp2Md.Domain.Tests.Facets;

public sealed class FacetAxesTests
{
    [Fact]
    [Trait("Requirement", "TAX-25")]
    public void WireValue_PairingIsTotalAndInjectiveForEveryFacetsAxis()
    {
        AssertPairingIsTotalAndInjective<BoundaryProtocol>();
        AssertPairingIsTotalAndInjective<BoundaryDirection>();
        AssertPairingIsTotalAndInjective<BoundaryRole>();
        AssertPairingIsTotalAndInjective<DataStoreTechnology>();
        AssertPairingIsTotalAndInjective<DataObjectForm>();
        AssertPairingIsTotalAndInjective<DataOperationKind>();
        AssertPairingIsTotalAndInjective<SymbolFacet>();
        AssertPairingIsTotalAndInjective<MappingStateKind>();
    }

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(BoundaryProtocol.Http, "http")]
    [InlineData(BoundaryProtocol.Grpc, "grpc")]
    [InlineData(BoundaryProtocol.Messaging, "messaging")]
    [InlineData(BoundaryProtocol.Cli, "cli")]
    [InlineData(BoundaryProtocol.Scheduler, "scheduler")]
    [InlineData(BoundaryProtocol.Function, "function")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_BoundaryProtocol(BoundaryProtocol value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(BoundaryDirection.Inbound, "inbound")]
    [InlineData(BoundaryDirection.Outbound, "outbound")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_BoundaryDirection(BoundaryDirection value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(BoundaryRole.Command, "command")]
    [InlineData(BoundaryRole.Query, "query")]
    [InlineData(BoundaryRole.Event, "event")]
    [InlineData(BoundaryRole.Stream, "stream")]
    [InlineData(BoundaryRole.Lifecycle, "lifecycle")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_BoundaryRole(BoundaryRole value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(DataStoreTechnology.Relational, "relational")]
    [InlineData(DataStoreTechnology.Document, "document")]
    [InlineData(DataStoreTechnology.KeyValue, "key-value")]
    [InlineData(DataStoreTechnology.Cache, "cache")]
    [InlineData(DataStoreTechnology.Unknown, "unknown")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_DataStoreTechnology(DataStoreTechnology value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(DataObjectForm.Table, "table")]
    [InlineData(DataObjectForm.View, "view")]
    [InlineData(DataObjectForm.Collection, "collection")]
    [InlineData(DataObjectForm.KeySpace, "key-space")]
    [InlineData(DataObjectForm.CacheRegion, "cache-region")]
    [InlineData(DataObjectForm.Unknown, "unknown")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_DataObjectForm(DataObjectForm value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(DataOperationKind.Read, "read")]
    [InlineData(DataOperationKind.Insert, "insert")]
    [InlineData(DataOperationKind.Update, "update")]
    [InlineData(DataOperationKind.Delete, "delete")]
    [InlineData(DataOperationKind.Execute, "execute")]
    [InlineData(DataOperationKind.Unknown, "unknown")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_DataOperationKind(DataOperationKind value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(SymbolFacet.Callable, "callable")]
    [InlineData(SymbolFacet.Controller, "controller")]
    [InlineData(SymbolFacet.Handler, "handler")]
    [InlineData(SymbolFacet.Repository, "repository")]
    [InlineData(SymbolFacet.Client, "client")]
    [InlineData(SymbolFacet.Service, "service")]
    [InlineData(SymbolFacet.Abstract, "abstract")]
    [InlineData(SymbolFacet.ExternallyReachable, "externally-reachable")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_SymbolFacet(SymbolFacet value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Theory]
    [Trait("Requirement", "TAX-25")]
    [InlineData(MappingStateKind.ExplicitConfirmation, "explicit-confirmation")]
    [InlineData(MappingStateKind.ConventionalCandidate, "conventional-candidate")]
    [InlineData(MappingStateKind.Unresolved, "unresolved")]
    public void WireValue_ResolvesTheDocumentedLiteralWireValue_MappingStateKind(MappingStateKind value, string expectedWireValue) =>
        Assert.Equal(expectedWireValue, FacetAxes.WireValue(value));

    [Fact]
    [Trait("Requirement", "TAX-25")]
    public void WireValue_UndefinedValueReachedByCast_IsRejectedNamingTheAxisAndTheValue()
    {
        var undefined = (BoundaryProtocol)99;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => FacetAxes.WireValue(undefined));

        Assert.Contains(nameof(BoundaryProtocol), exception.Message, StringComparison.Ordinal);
        Assert.Equal(undefined, exception.ActualValue);
    }

    [Fact]
    [Trait("Requirement", "TAX-26")]
    public void WireValue_UnknownResolvesToARegisteredWireValueOnAllThreePersistenceAxes()
    {
        Assert.Equal("unknown", FacetAxes.WireValue(DataStoreTechnology.Unknown));
        Assert.Equal("unknown", FacetAxes.WireValue(DataObjectForm.Unknown));
        Assert.Equal("unknown", FacetAxes.WireValue(DataOperationKind.Unknown));
    }

    private static void AssertPairingIsTotalAndInjective<TEnum>()
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var wireValues = values.Select(FacetAxes.WireValue).ToArray();

        Assert.Equal(values.Length, wireValues.Distinct(StringComparer.Ordinal).Count());
    }
}
