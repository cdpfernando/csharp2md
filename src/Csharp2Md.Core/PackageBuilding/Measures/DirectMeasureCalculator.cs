namespace Csharp2Md.Core.PackageBuilding.Measures;

internal static class DirectMeasureCalculator
{
    internal static ImmutableArray<ScopeMeasures> Calculate(IEnumerable<AggregatedDependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        var direct = dependencies.Where(x => x.Nature == DependencyNature.Direct).ToArray();
        return direct.SelectMany(x => new[] { (x.Scope, x.Source), (x.Scope, x.Target) }).Distinct()
            .OrderBy(x => x.Scope).ThenBy(x => x.Item2.Value, StringComparer.Ordinal)
            .Select(item => new ScopeMeasures(item.Scope, item.Item2,
                direct.Where(x => x.Scope == item.Scope && x.Target == item.Item2).Select(x => x.Source).Distinct().Count(),
                direct.Where(x => x.Scope == item.Scope && x.Source == item.Item2).Select(x => x.Target).Distinct().Count(),
                direct.Count(x => x.Scope == AggregationScope.Component && x.Source == item.Item2 && x.Source != x.Target),
                ImmutableArray<CycleHandle>.Empty, ImmutableArray<ImpactTarget>.Empty, new GapCounts(0, 0, 0))).ToImmutableArray();
    }
}
