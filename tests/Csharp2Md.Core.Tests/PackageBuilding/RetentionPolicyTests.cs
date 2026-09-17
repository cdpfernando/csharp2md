using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Retention;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class RetentionPolicyTests
{
    [Fact] [Trait("Requirement", "PKG-04")]
    public void Apply_RetainsGapAffectingJourney() => Assert.Equal("gap:relevant", Assert.Single(Apply().Gaps).CanonicalKey);
    [Fact] [Trait("Requirement", "PKG-04")]
    public void Apply_ExcludesGapOutsideJourney() => Assert.DoesNotContain(Apply().Gaps, x => x.CanonicalKey == "gap:orphan");
    [Fact] [Trait("Requirement", "PKG-04")]
    public void Apply_OrdersGapsByAffectedRootsThenCanonicalKey() => Assert.Equal(Apply().Gaps.Select(x => x.CanonicalKey).Order(StringComparer.Ordinal), Apply().Gaps.Select(x => x.CanonicalKey));
    [Fact] [Trait("Requirement", "PKG-03")]
    public void Apply_AddsIncomingSupportRelation() => Assert.Contains(Apply().Relations, x => x.CanonicalKey == "relation:caller-root");
    [Fact] [Trait("Requirement", "PKG-03")]
    public void Apply_AddsIncomingSupportEntity() => Assert.Contains(Apply().Entities, x => x.CanonicalKey == "entity:caller");
    [Fact] [Trait("Requirement", "PKG-06")]
    public void Apply_RetainsCitedProductionSource() => Assert.Equal("document:prod", Assert.Single(Apply().CitedSources).CanonicalKey);
    [Fact] [Trait("Requirement", "PKG-05")]
    public void Apply_ExcludesUncitedSource() => Assert.DoesNotContain(Apply().CitedSources, x => x.CanonicalKey == "document:uncited");
    [Fact] [Trait("Requirement", "PKG-05")]
    public void Apply_ExcludesCitedTestSourceWhenPolicyDisabled() => Assert.Empty(Apply(includeTests: false, testEvidence: true).CitedSources);
    [Fact] [Trait("Requirement", "PKG-08")]
    public void Apply_IncludesCitedTestSourceWhenPolicyEnabled() => Assert.Equal("document:test", Assert.Single(Apply(includeTests: true, testEvidence: true).CitedSources).CanonicalKey);
    [Fact] [Trait("Requirement", "PKG-08")]
    public void Apply_RecordsTestInclusionChoiceInMeasurements() => Assert.True(Apply(includeTests: true).Measurements.IncludesTests);
    [Fact] [Trait("Requirement", "PKG-09")]
    public void Apply_UsesOnlyObservedRelationPayloads() => Assert.Equal("relation:caller-root", Apply().Relations.First().CanonicalKey);
    [Fact] [Trait("Requirement", "MET-07")]
    public void Apply_PreservesGapKindsSeparately() => Assert.Equal(GapKind.Unknown, Assert.Single(Apply().Gaps).Kind);
    [Fact] [Trait("Requirement", "PKG-05")]
    public void Apply_ExcludesIncomingRelationFromATestProjectSourceByDefault() =>
        Assert.DoesNotContain(Apply(callerIsTestProject: true).Relations, x => x.CanonicalKey == "relation:caller-root");
    [Fact] [Trait("Requirement", "PKG-08")]
    public void Apply_IncludesIncomingRelationFromATestProjectSourceWhenPolicyEnabled() =>
        Assert.Contains(Apply(includeTests: true, callerIsTestProject: true).Relations, x => x.CanonicalKey == "relation:caller-root");

    private static RetainedGraph Apply(bool includeTests = false, bool testEvidence = false, bool callerIsTestProject = false)
    {
        var s = CanonicalIdentity.CreateSolution("app", "App.sln"); var p = CanonicalIdentity.CreateProject(s, "App.csproj"); var v = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci"); var span = new SourceSpan(1, 1, 1, 1);
        var document = testEvidence ? "document:test" : "document:prod"; var proof = new EvidenceRecord("evidence:one", document, v, span, "digest");
        var entities = new[] { new LogicalEntity(EntityKind.Component, "entity:root", "root", null), new LogicalEntity(EntityKind.Symbol, "entity:terminal", "terminal", null), new LogicalEntity(EntityKind.Symbol, "entity:caller", "caller", null), new LogicalEntity(EntityKind.Symbol, "entity:orphan", "orphan", null) };
        var occurrences = callerIsTestProject
            ? new[] { new VariantOccurrence("entity:caller", CanonicalIdentity.CreateProject(s, "App.Tests/App.Tests.csproj"), v, new LogicalLocator("CallerTest.cs", span, p), "shape", ["evidence:one"]) }
            : Array.Empty<VariantOccurrence>();
        var graph = new FactualGraph(s, [..entities], [..occurrences], [proof], [new FactualRelation("relation:root-terminal", "entity:root", "entity:terminal", "internal", ["evidence:one"]), new FactualRelation("relation:caller-root", "entity:caller", "entity:root", "internal", ["evidence:one"])], [new KnowledgeGap("gap:relevant", GapKind.Unknown, "unresolved", ["entity:root"], ["evidence:one"]), new KnowledgeGap("gap:orphan", GapKind.Candidate, "unresolved", ["entity:orphan"], ["evidence:one"])], [new SourceDocumentSnapshot("document:prod", new LogicalLocator("Prod.cs", span, p), false, "a"), new SourceDocumentSnapshot("document:test", new LogicalLocator("Test.cs", span, p), true, "b"), new SourceDocumentSnapshot("document:uncited", new LogicalLocator("Other.cs", span, p), false, "c")], new ExtractionMeasurements(0, 0));
        return RetentionPolicy.Apply(graph, RetainedGraphBuilder.Build(graph, includeTests), includeTests);
    }
}
