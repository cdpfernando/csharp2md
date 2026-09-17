using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Measures;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class DirectMeasureCalculatorTests
{
    [Fact][Trait("Requirement","MET-01")] public void Calculate_EmptyGraphHasNoMeasures()=>Assert.Empty(DirectMeasureCalculator.Calculate([]));
    [Fact][Trait("Requirement","MET-01")] public void Calculate_FanOutCountsDistinctTargets()=>Assert.Equal(2,For("a",[Edge("a","b"),Edge("a","c")]).FanOut);
    [Fact][Trait("Requirement","MET-01")] public void Calculate_FanOutDeduplicatesSameTarget()=>Assert.Equal(1,For("a",[Edge("a","b"),Edge("a","b")]).FanOut);
    [Fact][Trait("Requirement","MET-02")] public void Calculate_FanInCountsDistinctOrigins()=>Assert.Equal(2,For("b",[Edge("a","b"),Edge("c","b")]).FanIn);
    [Fact][Trait("Requirement","MET-02")] public void Calculate_FanInDeduplicatesSameOrigin()=>Assert.Equal(1,For("b",[Edge("a","b"),Edge("a","b")]).FanIn);
    [Fact][Trait("Requirement","MET-03")] public void Calculate_DoesNotChangeAggregatedOccurrenceCount()
    {
        var aggregated=DependencyAggregator.Aggregate([Contribution("r1","e1"),Contribution("r2","e2"),Contribution("r3","e3")]);
        Assert.Equal(3,Assert.Single(aggregated).OccurrenceCount);
        Assert.Equal(1,For("a",aggregated).FanOut);
        Assert.Equal(3,Assert.Single(aggregated).OccurrenceCount);
    }
    [Fact][Trait("Requirement","MET-04")] public void Calculate_ComponentEdgeCountsDistinctOwnership()=>Assert.Equal(1,For("a",[Edge("a","b",scope:AggregationScope.Component)]).CrossComponentEdges);
    [Fact][Trait("Requirement","MET-04")] public void Calculate_SelfComponentEdgeDoesNotCross()=>Assert.Equal(0,For("a",[Edge("a","a",scope:AggregationScope.Component)]).CrossComponentEdges);
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][Trait("Requirement","MET-01")]
    public void Calculate_KeepsScopesSeparate(int scopeValue) { var scope=(AggregationScope)scopeValue; Assert.Equal(scope,Assert.Single(DirectMeasureCalculator.Calculate([Edge("a","b",scope:scope)]).Where(x=>x.Entity.Value=="a")).Scope); }
    private static DependencyContribution Contribution(string relation,string evidence)=>new(AggregationScope.Document,new EntityHandle("a"),new EntityHandle("b"),DependencyCategory.Http,new VariantHandle("v"),new RelationHandle(relation),new EvidenceHandle(evidence),true);
    private static ScopeMeasures For(string entity,IEnumerable<AggregatedDependency> edges)=>Assert.Single(DirectMeasureCalculator.Calculate(edges).Where(x=>x.Entity.Value==entity));
    private static AggregatedDependency Edge(string source,string target,int occurrences=1,AggregationScope scope=AggregationScope.Document)=>new(scope,new EntityHandle(source),new EntityHandle(target),DependencyCategory.Http,DependencyNature.Direct,occurrences,[],[new RelationHandle(source+target)],[new EvidenceHandle("e")]);
}
