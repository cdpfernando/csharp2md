namespace Csharp2Md.Core.PackageBuilding.Measures;

internal sealed record DependencyContribution(AggregationScope Scope, EntityHandle Source, EntityHandle Target, DependencyCategory Category, VariantHandle Variant, RelationHandle Relation, EvidenceHandle Evidence, bool IsConfirmed);

internal static class DependencyAggregator
{
    internal static ImmutableArray<AggregatedDependency> Aggregate(IEnumerable<DependencyContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        return contributions.Where(x => x.IsConfirmed).GroupBy(x => (x.Scope, x.Source, x.Target, x.Category))
            .OrderBy(x => x.Key.Scope).ThenBy(x => x.Key.Source.Value, StringComparer.Ordinal).ThenBy(x => x.Key.Target.Value, StringComparer.Ordinal).ThenBy(x => x.Key.Category)
            .Select(group => new AggregatedDependency(group.Key.Scope, group.Key.Source, group.Key.Target, group.Key.Category, DependencyNature.Direct, group.Count(),
                group.Select(x => x.Variant).Distinct().OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(), group.Select(x => x.Relation).Distinct().OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(), group.Select(x => x.Evidence).Distinct().OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray())).ToImmutableArray();
    }
}
