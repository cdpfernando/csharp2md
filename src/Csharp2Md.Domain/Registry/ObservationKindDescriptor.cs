using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Domain.Registry;

public sealed record ObservationKindDescriptor(
    ObservationKind Kind,
    string WireName,
    EmissionTier Tier);

internal static class ObservationKindTable
{
    public static readonly ImmutableArray<ObservationKindDescriptor> All =
    [
        new(ObservationKind.Invocation, "invocation", EmissionTier.AlwaysWhenBindable),
        new(ObservationKind.ObjectCreation, "object-creation", EmissionTier.AlwaysWhenBindable),
        new(ObservationKind.TypeUsage, "type-usage", EmissionTier.AlwaysWhenBindable),
        new(ObservationKind.BaseType, "base-type", EmissionTier.AlwaysWhenBindable),
        new(ObservationKind.AttributeUsage, "attribute-usage", EmissionTier.AlwaysWhenBindable),
        new(ObservationKind.Assignment, "assignment", EmissionTier.RegisteredContextOnly),
        new(ObservationKind.Configuration, "configuration", EmissionTier.RegisteredContextOnly),
        new(ObservationKind.RouteDeclaration, "route-declaration", EmissionTier.RegisteredContextOnly),
        new(ObservationKind.MessageOperation, "message-operation", EmissionTier.RegisteredContextOnly),
        new(ObservationKind.DataAccess, "data-access", EmissionTier.RegisteredContextOnly),
    ];
}

public sealed partial record TaxonomyTables
{
    public ImmutableArray<ObservationKindDescriptor> ObservationKinds { get; init; } = [];
}
