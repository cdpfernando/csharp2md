using Csharp2Md.Core.Analysis; using Csharp2Md.Core.PackageBuilding; using Csharp2Md.Core.PackageBuilding.Measures; using Csharp2Md.Core.PackageBuilding.Retention;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class RetrievalModelBuilderTests
{
 [Fact][Trait("Requirement","NAV-05")] public void Build_OwnsTheRetainedGraphForBothWriters()=>Assert.NotNull(Build().RetainedGraph);
 [Fact][Trait("Requirement","NAV-05")] public void Build_DoesNotRequireSourceGraphForRetainedFacts()=>Assert.Single(Build().RetainedGraph!.Entities);
 [Fact][Trait("Requirement","DEP-07")] public void Build_OrdersDependenciesCanonically()=>Assert.Equal(Build().Dependencies.Select(x=>x.Source.Value).Order(StringComparer.Ordinal),Build().Dependencies.Select(x=>x.Source.Value));
 [Fact][Trait("Requirement","MET-08")] public void Build_MergesReverseImpactWithoutScore()=>Assert.Equal(1,Assert.Single(Build().Measures).ReverseImpact.Single().Depth);
 [Fact][Trait("Requirement","NAV-05")] public void Build_ExposesCanonicalRoot()=>Assert.Equal("entity:root",Assert.Single(Assert.Single(Build().Solutions).Roots).Value);
 [Fact][Trait("Requirement","NAV-05")] public void Build_PreservesSharedIndexes()=>Assert.Equal("identity",Build().Indexes.Identity);
 [Fact][Trait("Requirement","NAV-05")] public void Build_IsInputPermutationStable(){var a=Build();var b=Build();Assert.Equal(a.Dependencies.Select(x=>x.Source.Value),b.Dependencies.Select(x=>x.Source.Value));}
 [Fact][Trait("Requirement","MET-08")] public void Build_OmitsCompositeQualityProperty()=>Assert.DoesNotContain(typeof(RetrievalModel).GetProperties(),x=>x.Name.Contains("Score")||x.Name.Contains("Quality"));
 private static RetrievalModel Build(){var s=CanonicalIdentity.CreateSolution("app","App.sln");var root=new LogicalEntity(EntityKind.Component,"entity:root","root",null);var g=new FactualGraph(s,[root],[],[],[],[],[],new ExtractionMeasurements(0,0));var r=RetainedGraphBuilder.Build(g);var d=new AggregatedDependency(AggregationScope.Component,new EntityHandle("z"),new EntityHandle("a"),DependencyCategory.Http,DependencyNature.Direct,1,[],[],[]);return RetrievalModelBuilder.Build(g,r,[d],[new ScopeMeasures(AggregationScope.Component,new EntityHandle("z"),0,1,0,[],[],new GapCounts(0,0,0))],[new ImpactMeasures(AggregationScope.Component,new EntityHandle("z"),[new ImpactTarget(new EntityHandle("a"),1)],new GapCounts(0,0,0))],new NavigationIndexes("identity","roots","out","in","contracts","persistence","evidence"));}
}
