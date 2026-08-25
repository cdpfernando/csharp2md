using System.Reflection;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Tests.Facts;

public sealed class BoundaryFactsTests
{
    private static FactReference ComponentReference(string name) =>
        new(new FactId("component", $"id1:component;name={name}"), "Component");

    private static FactReference SymbolReference(string metadataName)
    {
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Payment", metadataName, 0, "global::System.Void");
        return new FactReference(new FactId("symbol", signature.Value), "Symbol");
    }

    private static StructuralLiteral Route(string value) => StructuralLiteral.Create(LiteralRole.Route, value, "route");

    [Fact]
    [Trait("Requirement", "TAX-27")]
    public void EntryPointAndBoundaryOperation_ExposeNoConversionCastOrFactoryFromEachOther()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var entryPointMembers = typeof(EntryPoint).GetMethods(flags);
        var boundaryOperationMembers = typeof(BoundaryOperation).GetMethods(flags);

        Assert.DoesNotContain(entryPointMembers, m => ReferencesType(m, typeof(BoundaryOperation)));
        Assert.DoesNotContain(boundaryOperationMembers, m => ReferencesType(m, typeof(EntryPoint)));
    }

    private static bool ReferencesType(MethodInfo method, Type type) =>
        method.ReturnType == type || method.GetParameters().Any(p => p.ParameterType == type);

    [Fact]
    [Trait("Requirement", "TAX-28")]
    public void OneSymbolReference_ParticipatesInAnEntryPointAndABoundaryOperationAtTheSameTime()
    {
        var symbol = SymbolReference("Charge");
        var component = ComponentReference("payments-api");

        var entryPoint = EntryPoint.Create(symbol, component);
        var boundaryOperation = BoundaryOperation.Create(
            symbol,
            component,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"));

        Assert.Equal(symbol, entryPoint.Symbol);
        Assert.Equal(symbol, boundaryOperation.Symbol);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_IdentityChangesWhenTheOwningComponentChanges()
    {
        var baseline = CreateOutbound(ComponentReference("payments-api"), "external", "GET", Route("/v1/charges"));
        var changed = CreateOutbound(ComponentReference("billing-api"), "external", "GET", Route("/v1/charges"));

        Assert.NotEqual(baseline.Reference, changed.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_IdentityChangesWhenTheDestinationScopeChanges()
    {
        var component = ComponentReference("payments-api");
        var baseline = CreateOutbound(component, "external", "GET", Route("/v1/charges"));
        var changed = CreateOutbound(component, "internal", "GET", Route("/v1/charges"));

        Assert.NotEqual(baseline.Reference, changed.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_IdentityChangesWhenTheHttpMethodChanges()
    {
        var component = ComponentReference("payments-api");
        var baseline = CreateOutbound(component, "external", "GET", Route("/v1/charges"));
        var changed = CreateOutbound(component, "external", "POST", Route("/v1/charges"));

        Assert.NotEqual(baseline.Reference, changed.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_IdentityChangesWhenTheRouteChanges()
    {
        var component = ComponentReference("payments-api");
        var baseline = CreateOutbound(component, "external", "GET", Route("/v1/charges"));
        var changed = CreateOutbound(component, "external", "GET", Route("/v1/refunds"));

        Assert.NotEqual(baseline.Reference, changed.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_IdentityChangesWhenDirectionOrProtocolDiffer()
    {
        var component = ComponentReference("payments-api");
        var outbound = CreateOutbound(component, "external", "GET", Route("/v1/charges"));
        var inbound = BoundaryOperation.Create(
            SymbolReference("Charge"),
            component,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "/v1/charges", "protocolOperationKey"));

        Assert.NotEqual(outbound.Reference, inbound.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_IdentityIsStableWhenOnlyTheSymbolChanges()
    {
        var component = ComponentReference("payments-api");
        var first = BoundaryOperation.Create(
            SymbolReference("Charge"), component, BoundaryDirection.Outbound, BoundaryProtocol.Http,
            "external", "GET", Route("/v1/charges"));
        var second = BoundaryOperation.Create(
            SymbolReference("Refund"), component, BoundaryDirection.Outbound, BoundaryProtocol.Http,
            "external", "GET", Route("/v1/charges"));

        Assert.Equal(first.Reference, second.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-30")]
    public void Inbound_IdentityUsesExactlyOwningComponentAndProtocolOperationKey()
    {
        var component = ComponentReference("payments-api");
        var key = StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey");

        var first = BoundaryOperation.Create(SymbolReference("Charge"), component, BoundaryDirection.Inbound, protocolOperationKey: key);
        var second = BoundaryOperation.Create(
            SymbolReference("DifferentSymbol"), component, BoundaryDirection.Inbound, BoundaryProtocol.Grpc, protocolOperationKey: key);

        Assert.Equal(first.Reference, second.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-30")]
    public void Inbound_DifferentProtocolOperationKey_ProducesADifferentIdentity()
    {
        var component = ComponentReference("payments-api");
        var first = BoundaryOperation.Create(
            SymbolReference("Charge"), component, BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"));
        var second = BoundaryOperation.Create(
            SymbolReference("Charge"), component, BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /refund", "protocolOperationKey"));

        Assert.NotEqual(first.Reference, second.Reference);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_RouteParameterIsAStructuralLiteral_NotARawStringOrUri()
    {
        var factory = typeof(BoundaryOperation).GetMethod(nameof(BoundaryOperation.Create), BindingFlags.Public | BindingFlags.Static)!;
        var routeParameter = factory.GetParameters().Single(p => p.Name == "route");

        Assert.Equal(typeof(StructuralLiteral?), routeParameter.ParameterType);
    }

    [Fact]
    [Trait("Requirement", "TAX-29")]
    public void OutboundHttp_WrongRouteLiteralRole_IsRejectedNamingRoute()
    {
        var wrongRole = StructuralLiteral.Create(LiteralRole.SchemaName, "/v1/charges", "route");

        var exception = Assert.Throws<ArgumentException>(() => CreateOutbound(
            ComponentReference("payments-api"), "external", "GET", wrongRole));

        Assert.Equal("route", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-30")]
    public void Inbound_WrongProtocolOperationKeyRole_IsRejectedNamingProtocolOperationKey()
    {
        var wrongRole = StructuralLiteral.Create(LiteralRole.Route, "/v1/charges", "protocolOperationKey");

        var exception = Assert.Throws<ArgumentException>(() => BoundaryOperation.Create(
            SymbolReference("Charge"), ComponentReference("payments-api"), BoundaryDirection.Inbound, protocolOperationKey: wrongRole));

        Assert.Equal("protocolOperationKey", exception.ParamName);
    }

    private static BoundaryOperation CreateOutbound(
        FactReference component, string destinationScope, string httpMethod, StructuralLiteral route) =>
        BoundaryOperation.Create(
            SymbolReference("Charge"), component, BoundaryDirection.Outbound, BoundaryProtocol.Http, destinationScope, httpMethod, route);
}
