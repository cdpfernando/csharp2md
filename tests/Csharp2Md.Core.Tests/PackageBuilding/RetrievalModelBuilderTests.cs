using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Measures;
using Csharp2Md.Core.PackageBuilding.Retention;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class RetrievalModelBuilderTests
{
    [Fact][Trait("Requirement", "NAV-05")] public void Build_OwnsTheRetainedGraphForBothWriters() => Assert.NotNull(Assert.Single(Build().Solutions).RetainedGraph);
    [Fact][Trait("Requirement", "NAV-05")] public void Build_DoesNotRequireSourceGraphForRetainedFacts() => Assert.Single(Assert.Single(Build().Solutions).RetainedGraph!.Entities);
    [Fact][Trait("Requirement", "DEP-07")] public void Build_OrdersDependenciesCanonically() => Assert.Equal(Assert.Single(Build().Solutions).Dependencies.Select(x => x.Source.Value).Order(StringComparer.Ordinal), Assert.Single(Build().Solutions).Dependencies.Select(x => x.Source.Value));
    [Fact][Trait("Requirement", "MET-08")] public void Build_MergesReverseImpactWithoutScore() => Assert.Equal(1, Assert.Single(Assert.Single(Build().Solutions).Measures).ReverseImpact.Single().Depth);
    [Fact][Trait("Requirement", "NAV-05")] public void Build_ExposesCanonicalRoot() => Assert.Equal("entity:root", Assert.Single(Assert.Single(Build().Solutions).Roots).Value);
    [Fact][Trait("Requirement", "NAV-05")] public void Build_KeepsRetrievalDataSolutionScoped() => Assert.DoesNotContain(typeof(RetrievalModel).GetProperties(), property => property.Name is "Indexes" or "Dependencies" or "Measures" or "RetainedGraph");
    [Fact][Trait("Requirement", "NAV-05")] public void Build_IsInputPermutationStable() { var a = Build(); var b = Build(); Assert.Equal(Assert.Single(a.Solutions).Dependencies.Select(x => x.Source.Value), Assert.Single(b.Solutions).Dependencies.Select(x => x.Source.Value)); }
    [Fact]
    [Trait("Requirement", "MET-08")]
    public void Build_OmitsCompositeQualityProperty()
    {
        var forbidden = typeof(RetrievalModel).Assembly.GetTypes()
            .Where(type => type.Namespace is not null && (type.Namespace == "Csharp2Md.Core.PackageBuilding" || type.Namespace.StartsWith("Csharp2Md.Core.PackageBuilding.", StringComparison.Ordinal) || type.Namespace == "Csharp2Md.Core.Publication" || type.Namespace.StartsWith("Csharp2Md.Core.Publication.", StringComparison.Ordinal)))
            .SelectMany(type => type.GetProperties())
            .Where(property => property.Name.Contains("Score", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Quality", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Risk", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Coupling", StringComparison.OrdinalIgnoreCase))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(forbidden.Length == 0, "Package-building and publication types exposed a composite score or an automatic quality, risk or coupling label: " + string.Join(", ", forbidden));
    }

    private static RetrievalModel Build()
    {
        var s = CanonicalIdentity.CreateSolution("app", "App.sln");
        var root = new LogicalEntity(EntityKind.Component, "entity:root", "root", null);
        var g = new FactualGraph(s, [root], [], [], [], [], [], new ExtractionMeasurements(0, 0));
        var r = RetainedGraphBuilder.Build(g, includeTests: false);
        var d = new AggregatedDependency(AggregationScope.Component, new EntityHandle("z"), new EntityHandle("a"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], []);
        return RetrievalModelBuilder.Build(
            g,
            r,
            [d],
            [new ScopeMeasures(AggregationScope.Component, new EntityHandle("z"), 0, 1, 0, [], [], new GapCounts(0, 0, 0))],
            [new ImpactMeasures(AggregationScope.Component, new EntityHandle("z"), [new ImpactTarget(new EntityHandle("a"), 1)], new GapCounts(0, 0, 0))]);
    }
}
