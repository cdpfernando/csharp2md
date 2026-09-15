using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Measures;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class CycleCalculatorTests
{
    [Fact][Trait("Requirement","MET-05")] public void Calculate_AcyclicGraphHasNoCycles()=>Assert.Empty(CycleCalculator.Calculate([E("a","b")]));
    [Fact][Trait("Requirement","MET-05")] public void Calculate_FindsTwoNodeCycle()=>Assert.Equal(["a","b"],Assert.Single(CycleCalculator.Calculate([E("a","b"),E("b","a")])).Members.Select(x=>x.Value));
    [Fact][Trait("Requirement","MET-05")] public void Calculate_FindsSelfCycle()=>Assert.Equal("a",Assert.Single(CycleCalculator.Calculate([E("a","a")])).Members.Single().Value);
    [Fact][Trait("Requirement","MET-05")] public void Calculate_ExcludesDisconnectedAcyclicEdge()=>Assert.Single(CycleCalculator.Calculate([E("a","b"),E("b","a"),E("c","d")]));
    [Fact][Trait("Requirement","MET-05")] public void Calculate_IsPermutationDeterministic(){var a=CycleCalculator.Calculate([E("a","b"),E("b","a")]);var b=CycleCalculator.Calculate([E("b","a"),E("a","b")]);Assert.Equal(a.Select(x=>x.Handle.Value),b.Select(x=>x.Handle.Value)); Assert.Equal(a.SelectMany(x=>x.Members).Select(x=>x.Value),b.SelectMany(x=>x.Members).Select(x=>x.Value));}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][Trait("Requirement","MET-05")]
    public void Calculate_DoesNotMixScopes(int value){var scope=(AggregationScope)value;var cycles=CycleCalculator.Calculate([E("a","b",scope),E("b","a",scope),E("x","y",AggregationScope.Document),E("y","x",AggregationScope.Document)]);Assert.Contains(cycles,x=>x.Scope==scope);}
    [Fact][Trait("Requirement","MET-05")] public void Calculate_OrdersMembersCanonically()=>Assert.Equal(["a","b","c"],Assert.Single(CycleCalculator.Calculate([E("c","a"),E("a","b"),E("b","c")])).Members.Select(x=>x.Value));
    private static AggregatedDependency E(string s,string t,AggregationScope scope=AggregationScope.Document)=>new(scope,new EntityHandle(s),new EntityHandle(t),DependencyCategory.Http,DependencyNature.Direct,1,[],[new RelationHandle(s+t)],[new EvidenceHandle("e")]);
}
