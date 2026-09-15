using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class RetrievalContractTests
{
    [Fact]
    [Trait("Requirement", "DEP-01")]
    public void AggregationScope_IsTheClosedSetOfFourProvenScopes()
    {
        Assert.Equal(
            [
                AggregationScope.Document,
                AggregationScope.Project,
                AggregationScope.Component,
                AggregationScope.DeploymentUnit,
            ],
            Enum.GetValues<AggregationScope>());
    }

    [Fact]
    [Trait("Requirement", "DEP-02")]
    public void DependencyCategory_IsTheClosedSetOfEightRelationCategories()
    {
        Assert.Equal(
            [
                DependencyCategory.ProjectReference,
                DependencyCategory.InternalInvocation,
                DependencyCategory.StructuralTypeUse,
                DependencyCategory.Http,
                DependencyCategory.Grpc,
                DependencyCategory.Messaging,
                DependencyCategory.Contract,
                DependencyCategory.Persistence,
            ],
            Enum.GetValues<DependencyCategory>());
    }

    [Fact]
    [Trait("Requirement", "DEP-03")]
    public void DirectDependencyAndTransitiveImpact_AreDistinctNatures()
    {
        var direct = Dependency(DependencyNature.Direct);
        var transitive = Dependency(DependencyNature.Transitive);

        Assert.Equal(DependencyNature.Direct, direct.Nature);
        Assert.Equal(DependencyNature.Transitive, transitive.Nature);
        Assert.NotEqual(direct, transitive);
        Assert.Equal(
            [DependencyNature.Direct, DependencyNature.Transitive],
            Enum.GetValues<DependencyNature>());
    }

    [Fact]
    [Trait("Requirement", "DEP-03")]
    public void AggregatedDependency_DeclaresSourceTargetScopeCategoryCountVariantsAndEvidence()
    {
        var dependency = Dependency(DependencyNature.Direct, occurrenceCount: 3);

        Assert.Equal(AggregationScope.Component, dependency.Scope);
        Assert.Equal("src-1", dependency.Source.Value);
        Assert.Equal("dst-1", dependency.Target.Value);
        Assert.Equal(DependencyCategory.Http, dependency.Category);
        Assert.Equal(3, dependency.OccurrenceCount);
        Assert.Equal("var-1", Assert.Single(dependency.Variants).Value);
        Assert.Equal("rel-1", Assert.Single(dependency.Relations).Value);
        Assert.Equal("ev-1", Assert.Single(dependency.Evidence).Value);
    }

    [Fact]
    [Trait("Requirement", "MET-01")]
    [Trait("Requirement", "MET-02")]
    public void ScopeMeasures_FanInAndFanOut_AreNonNegativeDistinctNeighborCounts()
    {
        var measures = Measures(fanIn: 2, fanOut: 5, gaps: new GapCounts(1, 0, 0));

        Assert.Equal(2, measures.FanIn);
        Assert.Equal(5, measures.FanOut);
        Assert.NotEqual(measures.FanIn, measures.FanOut);
    }

    [Fact]
    [Trait("Requirement", "MET-07")]
    public void ScopeMeasures_KeepGapCountsSeparateFromConfirmedFanInAndFanOut()
    {
        var measures = Measures(fanIn: 4, fanOut: 1, gaps: new GapCounts(7, 2, 1));

        Assert.Equal(4, measures.FanIn);
        Assert.Equal(1, measures.FanOut);
        Assert.Equal(7, measures.Gaps.Candidate);
        Assert.Equal(2, measures.Gaps.Unknown);
        Assert.Equal(1, measures.Gaps.OpenFrontier);
        Assert.NotEqual(measures.FanIn + measures.FanOut, measures.Gaps.Candidate);
    }

    [Fact]
    [Trait("Requirement", "MET-05")]
    [Trait("Requirement", "MET-06")]
    public void ScopeMeasures_CarryCycleMembershipAndReverseImpactSeparately()
    {
        var impact = new ImpactTarget(new EntityHandle("caller"), 2);
        var measures = new ScopeMeasures(
            AggregationScope.Project,
            new EntityHandle("orders"),
            fanIn: 1,
            fanOut: 1,
            crossComponentEdges: 0,
            ImmutableArray.Create(new CycleHandle("cycle-1")),
            ImmutableArray.Create(impact),
            new GapCounts(0, 0, 0));

        Assert.Equal("cycle-1", Assert.Single(measures.Cycles).Value);
        var target = Assert.Single(measures.ReverseImpact);
        Assert.Equal("caller", target.Entity.Value);
        Assert.Equal(2, target.Depth);
    }

    [Fact]
    [Trait("Requirement", "NAV-05")]
    public void RetrievalModel_OwnsDependenciesAndMeasuresFromTheSameRetainedSet()
    {
        var model = new RetrievalModel(
            ImmutableArray.Create(new SolutionNavigation(
                new SolutionIdentity("solution:acme", "src/Acme.sln"),
                ImmutableArray.Create(new EntityHandle("component:orders")))),
            ImmutableArray.Create(Dependency(DependencyNature.Direct)),
            ImmutableArray.Create(Measures(1, 1, new GapCounts(0, 0, 0))),
            new NavigationIndexes("identity", "roots", "outgoing", "incoming", "contracts", "persistence", "evidence"));

        Assert.Equal("component:orders", Assert.Single(Assert.Single(model.Solutions).Roots).Value);
        Assert.Equal(DependencyNature.Direct, Assert.Single(model.Dependencies).Nature);
        Assert.Equal(1, Assert.Single(model.Measures).FanIn);
        Assert.Equal("identity", model.Indexes.Identity);
    }

    [Theory]
    [Trait("Requirement", "DEP-03")]
    [InlineData(-1)]
    [InlineData(0)]
    public void AggregatedDependency_NonPositiveOccurrenceCount_IsRejected(int count)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Dependency(DependencyNature.Direct, count));
        Assert.Equal("occurrenceCount", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "MET-01")]
    public void ScopeMeasures_NegativeFanOut_IsRejected()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Measures(fanIn: 0, fanOut: -1, gaps: new GapCounts(0, 0, 0)));
        Assert.Equal("fanOut", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "DEP-01")]
    public void AggregatedDependency_UndefinedScope_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AggregatedDependency(
                (AggregationScope)42,
                new EntityHandle("src-1"),
                new EntityHandle("dst-1"),
                DependencyCategory.Http,
                DependencyNature.Direct,
                1,
                ImmutableArray<VariantHandle>.Empty,
                ImmutableArray<RelationHandle>.Empty,
                ImmutableArray<EvidenceHandle>.Empty));
    }

    [Fact]
    [Trait("Requirement", "NAV-05")]
    public void RetrievalModel_DefaultCollections_AreOwnedEmptyArrays()
    {
        var model = new RetrievalModel(
            default,
            default,
            default,
            new NavigationIndexes("identity", "roots", "outgoing", "incoming", "contracts", "persistence", "evidence"));

        Assert.False(model.Dependencies.IsDefault);
        Assert.Empty(model.Dependencies);
        Assert.Empty(model.Measures);
        Assert.Empty(model.Solutions);
    }

    private static AggregatedDependency Dependency(DependencyNature nature, int occurrenceCount = 1) =>
        new(
            AggregationScope.Component,
            new EntityHandle("src-1"),
            new EntityHandle("dst-1"),
            DependencyCategory.Http,
            nature,
            occurrenceCount,
            ImmutableArray.Create(new VariantHandle("var-1")),
            ImmutableArray.Create(new RelationHandle("rel-1")),
            ImmutableArray.Create(new EvidenceHandle("ev-1")));

    private static ScopeMeasures Measures(int fanIn, int fanOut, GapCounts gaps) =>
        new(
            AggregationScope.Component,
            new EntityHandle("orders"),
            fanIn,
            fanOut,
            crossComponentEdges: 0,
            ImmutableArray<CycleHandle>.Empty,
            ImmutableArray<ImpactTarget>.Empty,
            gaps);
}
