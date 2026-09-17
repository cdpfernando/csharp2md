namespace Csharp2Md.Core.PackageBuilding.Measures;

internal sealed class DirectedCycle : IEquatable<DirectedCycle>
{
    public DirectedCycle(CycleHandle handle, AggregationScope scope, ImmutableArray<EntityHandle> members) { Handle = handle; Scope = scope; Members = members.IsDefault ? ImmutableArray<EntityHandle>.Empty : members; }
    public CycleHandle Handle { get; } public AggregationScope Scope { get; } public ImmutableArray<EntityHandle> Members { get; }
    public bool Equals(DirectedCycle? other) => other is not null && Handle == other.Handle && Scope == other.Scope && Members.SequenceEqual(other.Members);
    public override bool Equals(object? obj) => Equals(obj as DirectedCycle);
    public override int GetHashCode() => HashCode.Combine(Handle, Scope, Members.Aggregate(0, HashCode.Combine));
}

internal static class CycleCalculator
{
    internal static ImmutableArray<DirectedCycle> Calculate(IEnumerable<AggregatedDependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        var result = new List<DirectedCycle>();
        foreach (var scope in Enum.GetValues<AggregationScope>())
        {
            var edges = dependencies.Where(x => x.Scope == scope && x.Nature == DependencyNature.Direct).ToArray();
            var nodes = edges.SelectMany(x => new[] { x.Source, x.Target }).Distinct().OrderBy(x => x.Value, StringComparer.Ordinal).ToArray();
            var index = 0; var stack = new Stack<EntityHandle>(); var onStack = new HashSet<EntityHandle>(); var indices = new Dictionary<EntityHandle,int>(); var lows = new Dictionary<EntityHandle,int>();
            void Visit(EntityHandle node) { indices[node]=lows[node]=index++; stack.Push(node); onStack.Add(node); foreach(var next in edges.Where(x=>x.Source==node).Select(x=>x.Target).OrderBy(x=>x.Value,StringComparer.Ordinal)) { if(!indices.ContainsKey(next)){Visit(next); lows[node]=Math.Min(lows[node],lows[next]);} else if(onStack.Contains(next)) lows[node]=Math.Min(lows[node],indices[next]); } if(lows[node]!=indices[node]) return; var members=new List<EntityHandle>(); EntityHandle item; do { item=stack.Pop(); onStack.Remove(item); members.Add(item); } while(item!=node); members.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Value,b.Value)); if(members.Count>1 || edges.Any(x=>x.Source==node&&x.Target==node)) result.Add(new DirectedCycle(new CycleHandle("cycle:"+scope.ToString().ToLowerInvariant()+":"+string.Join(',',members.Select(x=>x.Value))),scope,[..members])); }
            foreach(var node in nodes) if(!indices.ContainsKey(node)) Visit(node);
        }
        return result.OrderBy(x=>x.Scope).ThenBy(x=>x.Handle.Value,StringComparer.Ordinal).ToImmutableArray();
    }
}
