using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Classification;

/// <summary>
/// Cuts a relation's <c>derived_from</c> down to the observations that justify that specific
/// promotion, instead of every observation a caller happens to have on hand (AD-027, GCPC-039).
/// A structural relation (e.g. <see cref="RelationKind.Contains"/>) is justified by declaration-shape
/// evidence, so behavioral occurrences -- <see cref="ObservationKind.Invocation"/> and
/// <see cref="ObservationKind.DataAccess"/> -- are never relevant to it and are excluded. A causal
/// relation (e.g. <see cref="RelationKind.Invokes"/>) is justified by the occurrence that produced
/// it, so the caller's candidate set is trusted as-is: narrowing which occurrence caused a specific
/// causal edge is the caller's responsibility, not this scope's.
/// </summary>
internal static class EvidenceScope
{
    private static readonly ImmutableHashSet<ObservationKind> BehavioralNoiseForStructuralRelations =
        ImmutableHashSet.Create(ObservationKind.Invocation, ObservationKind.DataAccess);

    private static readonly ImmutableHashSet<RelationKind> CausalRelationKinds = ImmutableHashSet.Create(
        RelationKind.Invokes,
        RelationKind.Executes,
        RelationKind.ImplementsOperation,
        RelationKind.UsesContract,
        RelationKind.AccessesData,
        RelationKind.OperatesOn);

    public static EvidenceChain For(
        FactReference source, FactReference target, RelationKind kind, IEnumerable<Observation> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        RequireInitialized(source, nameof(source));
        RequireInitialized(target, nameof(target));

        var scoped = CausalRelationKinds.Contains(kind)
            ? candidates
            : candidates.Where(static observation =>
                !BehavioralNoiseForStructuralRelations.Contains(observation.Identity.Kind));

        return EvidenceChain.Create(scoped.Select(static observation => observation.Identity));
    }

    private static void RequireInitialized(FactReference reference, string parameterName)
    {
        if (reference.Equals(default(FactReference)))
        {
            throw new ArgumentException("A relation endpoint must be an initialized fact reference.", parameterName);
        }
    }
}
