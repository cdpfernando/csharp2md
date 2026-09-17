using Csharp2Md.Core.Analysis; using Csharp2Md.Core.PackageBuilding; using Csharp2Md.Core.PackageBuilding.Measures;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class ImpactCalculatorTests
{
 [Fact][Trait("Requirement","MET-06")] public void Calculate_UsesMinimumDepth()=>Assert.Equal(1,For("c",[E("a","b"),E("b","c"),E("a","c")],[]).ReverseImpact.Single(x=>x.Entity.Value=="a").Depth);
 [Fact][Trait("Requirement","MET-06")] public void Calculate_ListsReachableSourceOnce()=>Assert.Single(For("c",[E("a","b"),E("b","c"),E("a","c")],[]).ReverseImpact.Where(x=>x.Entity.Value=="a"));
 [Fact][Trait("Requirement","MET-06")] public void Calculate_HandlesDiamond()=>Assert.Equal(3,For("d",[E("a","b"),E("a","c"),E("b","d"),E("c","d")],[]).ReverseImpact.Count());
 [Fact][Trait("Requirement","MET-06")] public void Calculate_HandlesCycle()=>Assert.Contains(For("a",[E("a","b"),E("b","a")],[]).ReverseImpact,x=>x.Entity.Value=="b"&&x.Depth==1);
 [Fact][Trait("Requirement","MET-07")] public void Calculate_CountsCandidateSeparately()=>Assert.Equal(1,For("a",[E("a","b")],[G("g",GapKind.Candidate,"a")]).Gaps.Candidate);
 [Fact][Trait("Requirement","MET-07")] public void Calculate_CountsUnknownSeparately()=>Assert.Equal(1,For("a",[E("a","b")],[G("g",GapKind.Unknown,"a")]).Gaps.Unknown);
 [Fact][Trait("Requirement","MET-07")] public void Calculate_CountsFrontierSeparately()=>Assert.Equal(1,For("a",[E("a","b")],[G("g",GapKind.OpenFrontier,"a")]).Gaps.OpenFrontier);
 [Fact][Trait("Requirement","MET-07")] public void Calculate_OmitsUnrelatedGap()=>Assert.Equal(0,For("a",[E("a","b")],[G("g",GapKind.Unknown,"other")]).Gaps.Unknown);
 [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][Trait("Requirement","MET-06")] public void Calculate_SeparatesScopes(int value){var scope=(AggregationScope)value; Assert.Empty(For("b",[E("a","b",scope)],[]).ReverseImpact.Where(x=>x.Entity.Value=="x"));}
 private static ImpactMeasures For(string entity,IEnumerable<AggregatedDependency> edges,IEnumerable<KnowledgeGap> gaps)=>Assert.Single(ImpactCalculator.Calculate(edges,gaps).Where(x=>x.Entity.Value==entity));
 private static AggregatedDependency E(string a,string b,AggregationScope scope=AggregationScope.Document)=>new(scope,new EntityHandle(a),new EntityHandle(b),DependencyCategory.Http,DependencyNature.Direct,1,[],[],[]);
 private static KnowledgeGap G(string key,GapKind kind,string entity)=>new(key,kind,"cause",[entity],[]);
}
