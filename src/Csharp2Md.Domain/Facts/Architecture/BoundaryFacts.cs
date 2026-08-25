using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Facts;

public sealed record EntryPoint : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Architecture;

    public FactReference Symbol { get; }

    public FactReference OwningComponent { get; }

    private EntryPoint(FactReference reference, FactReference symbol, FactReference owningComponent)
    {
        Reference = reference;
        Symbol = symbol;
        OwningComponent = owningComponent;
    }

    public static EntryPoint Create(FactReference symbol, FactReference owningComponent)
    {
        FactGuards.RequireInitialized(symbol, nameof(symbol));
        FactGuards.RequireInitialized(owningComponent, nameof(owningComponent));

        var id = FactIdGrammar.Create("entry-point", ("component", owningComponent.Id.Value), ("symbol", symbol.Id.Value));
        var reference = new FactReference(id, nameof(EntryPoint));
        return new EntryPoint(reference, symbol, owningComponent);
    }
}

public sealed record BoundaryOperation : IFact
{
    public FactReference Reference { get; }

    public FactFamily Family => FactFamily.Architecture;

    public FactReference Symbol { get; }

    public FactReference OwningComponent { get; }

    public BoundaryDirection Direction { get; }

    public BoundaryProtocol? Protocol { get; }

    public string? DestinationScope { get; }

    public string? HttpMethod { get; }

    public StructuralLiteral? Route { get; }

    public StructuralLiteral? ProtocolOperationKey { get; }

    private BoundaryOperation(
        FactReference reference,
        FactReference symbol,
        FactReference owningComponent,
        BoundaryDirection direction,
        BoundaryProtocol? protocol,
        string? destinationScope,
        string? httpMethod,
        StructuralLiteral? route,
        StructuralLiteral? protocolOperationKey)
    {
        Reference = reference;
        Symbol = symbol;
        OwningComponent = owningComponent;
        Direction = direction;
        Protocol = protocol;
        DestinationScope = destinationScope;
        HttpMethod = httpMethod;
        Route = route;
        ProtocolOperationKey = protocolOperationKey;
    }

    public static BoundaryOperation Create(
        FactReference symbol,
        FactReference owningComponent,
        BoundaryDirection direction,
        BoundaryProtocol? protocol = null,
        string? destinationScope = null,
        string? httpMethod = null,
        StructuralLiteral? route = null,
        StructuralLiteral? protocolOperationKey = null)
    {
        FactGuards.RequireInitialized(symbol, nameof(symbol));
        FactGuards.RequireInitialized(owningComponent, nameof(owningComponent));
        FactGuards.RequireDefined(direction, nameof(direction));

        return (direction, protocol) switch
        {
            (BoundaryDirection.Outbound, BoundaryProtocol.Http) => CreateOutboundHttp(
                symbol, owningComponent, destinationScope, httpMethod, route),
            (BoundaryDirection.Inbound, _) => CreateInbound(symbol, owningComponent, protocol, protocolOperationKey),
            _ => throw new ArgumentException(
                $"Direction '{direction}' with protocol '{protocol}' is not a supported boundary-operation combination.",
                nameof(direction)),
        };
    }

    private static BoundaryOperation CreateOutboundHttp(
        FactReference symbol,
        FactReference owningComponent,
        string? destinationScope,
        string? httpMethod,
        StructuralLiteral? route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationScope, nameof(destinationScope));
        var canonicalScope = FactIdGrammar.RequireCanonicalText(destinationScope, nameof(destinationScope));
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod, nameof(httpMethod));
        var canonicalMethod = FactIdGrammar.RequireCanonicalText(httpMethod, nameof(httpMethod));
        if (route is null)
        {
            throw new ArgumentException("An outbound HTTP boundary operation requires a route.", nameof(route));
        }

        if (route.Value.Role != LiteralRole.Route)
        {
            throw new ArgumentException(
                $"An outbound HTTP boundary operation's route must be a structural literal with role '{nameof(LiteralRole.Route)}', but was '{route.Value.Role}'.",
                nameof(route));
        }

        var operationKey = string.Join(
            '\u0000',
            FacetAxes.WireValue(BoundaryDirection.Outbound),
            FacetAxes.WireValue(BoundaryProtocol.Http),
            canonicalScope,
            canonicalMethod,
            route.Value.Value);

        var id = FactIdGrammar.Create("boundary-operation", ("component", owningComponent.Id.Value), ("operation-key", operationKey));
        var reference = new FactReference(id, nameof(BoundaryOperation));
        return new BoundaryOperation(
            reference, symbol, owningComponent, BoundaryDirection.Outbound, BoundaryProtocol.Http, canonicalScope, canonicalMethod, route, null);
    }

    private static BoundaryOperation CreateInbound(
        FactReference symbol,
        FactReference owningComponent,
        BoundaryProtocol? protocol,
        StructuralLiteral? protocolOperationKey)
    {
        if (protocolOperationKey is null)
        {
            throw new ArgumentException("An inbound boundary operation requires a protocol operation key.", nameof(protocolOperationKey));
        }

        if (protocolOperationKey.Value.Role != LiteralRole.ProtocolName)
        {
            throw new ArgumentException(
                $"An inbound boundary operation's protocol operation key must be a structural literal with role '{nameof(LiteralRole.ProtocolName)}', but was '{protocolOperationKey.Value.Role}'.",
                nameof(protocolOperationKey));
        }

        if (protocol is not null)
        {
            FactGuards.RequireDefined(protocol.Value, nameof(protocol));
        }

        var id = FactIdGrammar.Create(
            "boundary-operation", ("component", owningComponent.Id.Value), ("operation-key", protocolOperationKey.Value.Value));
        var reference = new FactReference(id, nameof(BoundaryOperation));
        return new BoundaryOperation(
            reference, symbol, owningComponent, BoundaryDirection.Inbound, protocol, null, null, null, protocolOperationKey);
    }
}
