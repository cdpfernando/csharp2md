using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding.Measures;
namespace Csharp2Md.Core.PackageBuilding;
internal static class RetrievalModelBuilder
{
    internal static RetrievalModel Build(FactualGraph graph, RetainedGraph retained, IEnumerable<AggregatedDependency> dependencies, IEnumerable<ScopeMeasures> directMeasures, IEnumerable<ImpactMeasures> impactMeasures)
    {
        ArgumentNullException.ThrowIfNull(graph); ArgumentNullException.ThrowIfNull(retained); ArgumentNullException.ThrowIfNull(dependencies); ArgumentNullException.ThrowIfNull(directMeasures); ArgumentNullException.ThrowIfNull(impactMeasures);
        var impacts=impactMeasures.ToDictionary(x=>(x.Scope,x.Entity));
        var measures=directMeasures.Select(x=> impacts.TryGetValue((x.Scope,x.Entity),out var impact) ? new ScopeMeasures(x.Scope,x.Entity,x.FanIn,x.FanOut,x.CrossComponentEdges,x.Cycles,impact.ReverseImpact,impact.Gaps) : x).OrderBy(x=>x.Scope).ThenBy(x=>x.Entity.Value,StringComparer.Ordinal).ToImmutableArray();
        var roots=retained.Entities.Where(x=>x.Kind is EntityKind.Component or EntityKind.DeploymentUnit or EntityKind.EntryPoint or EntityKind.BoundaryOperation).Select(x=>new EntityHandle(x.CanonicalKey)).OrderBy(x=>x.Value,StringComparer.Ordinal).ToImmutableArray();
        var solution = new SolutionRetrievalModel(graph.Solution, roots, dependencies.OrderBy(x=>x.Scope).ThenBy(x=>x.Source.Value,StringComparer.Ordinal).ThenBy(x=>x.Target.Value,StringComparer.Ordinal).ThenBy(x=>x.Category).ToImmutableArray(), measures, retained);
        return new RetrievalModel([solution]);
    }
}
