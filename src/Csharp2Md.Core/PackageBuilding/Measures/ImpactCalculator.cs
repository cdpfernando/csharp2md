using Csharp2Md.Core.Analysis;
namespace Csharp2Md.Core.PackageBuilding.Measures;
internal sealed record ImpactMeasures(AggregationScope Scope, EntityHandle Entity, ImmutableArray<ImpactTarget> ReverseImpact, GapCounts Gaps);
internal static class ImpactCalculator
{
    internal static ImmutableArray<ImpactMeasures> Calculate(IEnumerable<AggregatedDependency> dependencies, IEnumerable<KnowledgeGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(dependencies); ArgumentNullException.ThrowIfNull(gaps); var edges=dependencies.Where(x=>x.Nature==DependencyNature.Direct).ToArray(); var gapList=gaps.ToArray();
        return edges.SelectMany(x=>new[]{(x.Scope,x.Source),(x.Scope,x.Target)}).Distinct().OrderBy(x=>x.Scope).ThenBy(x=>x.Item2.Value,StringComparer.Ordinal).Select(item=>{
            var found=new Dictionary<EntityHandle,int>(); var queue=new Queue<(EntityHandle,int)>(); queue.Enqueue((item.Item2,0)); while(queue.TryDequeue(out var next)) foreach(var edge in edges.Where(x=>x.Scope==item.Scope&&x.Target==next.Item1)) if(!found.TryGetValue(edge.Source,out var known)||next.Item2+1<known){found[edge.Source]=next.Item2+1;queue.Enqueue((edge.Source,next.Item2+1));}
            found.Remove(item.Item2); var counts=new GapCounts(gapList.Count(x=>x.Kind==GapKind.Candidate&&x.AffectedEntityCanonicalKeys.Contains(item.Item2.Value)),gapList.Count(x=>x.Kind==GapKind.Unknown&&x.AffectedEntityCanonicalKeys.Contains(item.Item2.Value)),gapList.Count(x=>x.Kind==GapKind.OpenFrontier&&x.AffectedEntityCanonicalKeys.Contains(item.Item2.Value)));
            return new ImpactMeasures(item.Scope,item.Item2,found.OrderBy(x=>x.Value).ThenBy(x=>x.Key.Value,StringComparer.Ordinal).Select(x=>new ImpactTarget(x.Key,x.Value)).ToImmutableArray(),counts);}).ToImmutableArray();
    }
}
