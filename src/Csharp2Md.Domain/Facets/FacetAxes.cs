namespace Csharp2Md.Domain.Facets;

public static class FacetAxes
{
    public static string WireValue<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"'{value}' is not a defined value of the '{typeof(TEnum).Name}' axis.");
        }

        return value switch
        {
            BoundaryProtocol protocol => WireValueOf(protocol),
            BoundaryDirection direction => WireValueOf(direction),
            BoundaryRole role => WireValueOf(role),
            DataStoreTechnology technology => WireValueOf(technology),
            DataObjectForm form => WireValueOf(form),
            DataOperationKind operation => WireValueOf(operation),
            SymbolFacet facet => WireValueOf(facet),
            MappingStateKind mappingState => WireValueOf(mappingState),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"'{typeof(TEnum).Name}' has no registered wire-value pairing."),
        };
    }

    private static string WireValueOf(BoundaryProtocol protocol) => protocol switch
    {
        BoundaryProtocol.Http => "http",
        BoundaryProtocol.Grpc => "grpc",
        BoundaryProtocol.Messaging => "messaging",
        BoundaryProtocol.Cli => "cli",
        BoundaryProtocol.Scheduler => "scheduler",
        BoundaryProtocol.Function => "function",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, $"'{protocol}' is not a defined value of the '{nameof(BoundaryProtocol)}' axis."),
    };

    private static string WireValueOf(BoundaryDirection direction) => direction switch
    {
        BoundaryDirection.Inbound => "inbound",
        BoundaryDirection.Outbound => "outbound",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, $"'{direction}' is not a defined value of the '{nameof(BoundaryDirection)}' axis."),
    };

    private static string WireValueOf(BoundaryRole role) => role switch
    {
        BoundaryRole.Command => "command",
        BoundaryRole.Query => "query",
        BoundaryRole.Event => "event",
        BoundaryRole.Stream => "stream",
        BoundaryRole.Lifecycle => "lifecycle",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, $"'{role}' is not a defined value of the '{nameof(BoundaryRole)}' axis."),
    };

    private static string WireValueOf(DataStoreTechnology technology) => technology switch
    {
        DataStoreTechnology.Relational => "relational",
        DataStoreTechnology.Document => "document",
        DataStoreTechnology.KeyValue => "key-value",
        DataStoreTechnology.Cache => "cache",
        DataStoreTechnology.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(technology), technology, $"'{technology}' is not a defined value of the '{nameof(DataStoreTechnology)}' axis."),
    };

    private static string WireValueOf(DataObjectForm form) => form switch
    {
        DataObjectForm.Table => "table",
        DataObjectForm.View => "view",
        DataObjectForm.Collection => "collection",
        DataObjectForm.KeySpace => "key-space",
        DataObjectForm.CacheRegion => "cache-region",
        DataObjectForm.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, $"'{form}' is not a defined value of the '{nameof(DataObjectForm)}' axis."),
    };

    private static string WireValueOf(DataOperationKind operation) => operation switch
    {
        DataOperationKind.Read => "read",
        DataOperationKind.Insert => "insert",
        DataOperationKind.Update => "update",
        DataOperationKind.Delete => "delete",
        DataOperationKind.Execute => "execute",
        DataOperationKind.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, $"'{operation}' is not a defined value of the '{nameof(DataOperationKind)}' axis."),
    };

    private static string WireValueOf(SymbolFacet facet) => facet switch
    {
        SymbolFacet.Callable => "callable",
        SymbolFacet.Controller => "controller",
        SymbolFacet.Handler => "handler",
        SymbolFacet.Repository => "repository",
        SymbolFacet.Client => "client",
        SymbolFacet.Service => "service",
        SymbolFacet.Abstract => "abstract",
        SymbolFacet.ExternallyReachable => "externally-reachable",
        _ => throw new ArgumentOutOfRangeException(nameof(facet), facet, $"'{facet}' is not a defined value of the '{nameof(SymbolFacet)}' axis."),
    };

    private static string WireValueOf(MappingStateKind mappingState) => mappingState switch
    {
        MappingStateKind.ExplicitConfirmation => "explicit-confirmation",
        MappingStateKind.ConventionalCandidate => "conventional-candidate",
        MappingStateKind.Unresolved => "unresolved",
        _ => throw new ArgumentOutOfRangeException(nameof(mappingState), mappingState, $"'{mappingState}' is not a defined value of the '{nameof(MappingStateKind)}' axis."),
    };
}
