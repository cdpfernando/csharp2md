using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Measures;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class DependencyAggregatorTests
{
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [Trait("Requirement","DEP-01")]
    public void Aggregate_KeepsEachProvenScope(int scope) => Assert.Equal((AggregationScope)scope, Assert.Single(DependencyAggregator.Aggregate([Item((AggregationScope)scope)])).Scope);
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [Trait("Requirement","DEP-02")]
    public void Aggregate_KeepsEachCategory(int category) => Assert.Equal((DependencyCategory)category, Assert.Single(DependencyAggregator.Aggregate([Item(category:(DependencyCategory)category)])).Category);
    [Fact] [Trait("Requirement","DEP-04")] public void Aggregate_MergesSameEdgeAndCountsOccurrences() => Assert.Equal(2, Assert.Single(DependencyAggregator.Aggregate([Item(),Item()])).OccurrenceCount);
    [Fact] [Trait("Requirement","DEP-04")] public void Aggregate_DeduplicatesEvidenceAndRelationReferences() { var edge=Assert.Single(DependencyAggregator.Aggregate([Item(),Item()])); Assert.Single(edge.Evidence); Assert.Single(edge.Relations); }
    [Fact] [Trait("Requirement","DEP-06")] public void Aggregate_ExcludesUnconfirmedContribution() => Assert.Empty(DependencyAggregator.Aggregate([Item(confirmed:false)]));
    [Fact] [Trait("Requirement","DEP-03")] public void Aggregate_RecordsVariantReferences() => Assert.Equal("v",Assert.Single(Assert.Single(DependencyAggregator.Aggregate([Item()])).Variants).Value);
    private static DependencyContribution Item(AggregationScope scope=AggregationScope.Document, DependencyCategory category=DependencyCategory.Http,bool confirmed=true) => new(scope,new EntityHandle("source"),new EntityHandle("target"),category,new VariantHandle("v"),new RelationHandle("r"),new EvidenceHandle("e"),confirmed);
}
