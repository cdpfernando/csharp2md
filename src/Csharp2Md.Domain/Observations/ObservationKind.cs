namespace Csharp2Md.Domain.Observations;

public enum ObservationKind
{
    Invocation,
    ObjectCreation,
    TypeUsage,
    BaseType,
    AttributeUsage,
    Assignment,
    Configuration,
    RouteDeclaration,
    MessageOperation,
    DataAccess,
}

public enum EmissionTier
{
    AlwaysWhenBindable,
    RegisteredContextOnly,
}
