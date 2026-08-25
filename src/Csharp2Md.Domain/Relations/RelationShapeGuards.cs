using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Relations;

public static class RelationShapeGuards
{
    private const string PayloadRoleAxisName = "payload-role";

    private static readonly ImmutableHashSet<RelationKind> CallableRequiringRelations = ImmutableHashSet.Create(
        RelationKind.Executes,
        RelationKind.Invokes,
        RelationKind.ImplementsOperation,
        RelationKind.AccessesData);

    public static void RequireCallableIfNeeded(RelationKind kind, Symbol symbol, string role)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        if (!CallableRequiringRelations.Contains(kind))
        {
            return;
        }

        if (!symbol.Facets.Facets.Contains(SymbolFacet.Callable))
        {
            throw new ArgumentException(
                $"Relation '{kind}' requires its {role} symbol to carry the '{nameof(SymbolFacet.Callable)}' facet.",
                role);
        }
    }

    public static void RequirePayloadRoleForUsesContract(RelationKind kind, FacetBinding facets)
    {
        if (kind != RelationKind.UsesContract)
        {
            return;
        }

        var hasRegisteredPayloadRole = facets.Entries.Any(entry =>
            string.Equals(entry.AxisName, PayloadRoleAxisName, StringComparison.Ordinal)
            && PayloadRoleTable.All.Contains(entry.WireValue, StringComparer.Ordinal));

        if (!hasRegisteredPayloadRole)
        {
            throw new ArgumentException(
                $"Relation '{kind}' requires a registered '{PayloadRoleAxisName}' facet.",
                nameof(facets));
        }
    }

    public static void RequireLegalTargetShape(RelationKind kind, IFact target)
    {
        ArgumentNullException.ThrowIfNull(target);

        switch (kind)
        {
            case RelationKind.Targets:
                RequireLegalTargetsShape(target);
                break;
            case RelationKind.OperatesOn:
                RequireLegalOperatesOnShape(target);
                break;
        }
    }

    public static void RequireSufficientEvidence(TaxonomyRegistry registry, RelationKind kind, EvidenceMethod supplied)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.RequireSufficientEvidence(kind, supplied);
    }

    private static void RequireLegalTargetsShape(IFact target)
    {
        var isLegal = target switch
        {
            BoundaryOperation operation => operation.Direction == BoundaryDirection.Inbound,
            DeploymentUnit => true,
            ExternalSystem => true,
            _ => false,
        };

        if (!isLegal)
        {
            throw new ArgumentException(
                $"Relation '{RelationKind.Targets}' requires an inbound boundary operation, a deployment unit or an external system as its target, but was '{Describe(target)}'.",
                nameof(target));
        }
    }

    private static void RequireLegalOperatesOnShape(IFact target)
    {
        if (target is not (DataObject or DataField))
        {
            throw new ArgumentException(
                $"Relation '{RelationKind.OperatesOn}' requires a data object or a data field as its target, but was '{Describe(target)}'.",
                nameof(target));
        }
    }

    private static string Describe(IFact fact) =>
        fact is BoundaryOperation operation
            ? $"{operation.Reference.FactType} ({operation.Direction})"
            : fact.Reference.FactType;
}
