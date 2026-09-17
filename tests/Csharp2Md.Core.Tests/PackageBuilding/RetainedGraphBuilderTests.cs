using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding.Retention;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class RetainedGraphBuilderTests
{
    [Theory]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    [Trait("Requirement", "PKG-02")]
    public void Build_StartsClosureAtEveryProvenRootKind(int kindValue)
    {
        var kind = (EntityKind)kindValue;
        var result = RetainedGraphBuilder.Build(Graph(kind), includeTests: false);
        Assert.Contains(result.Entities, entity => entity.Kind == kind);
        Assert.Contains(result.Entities, entity => entity.DisplayName == "terminal");
    }

    [Fact] [Trait("Requirement", "PKG-03")]
    public void Build_ExcludesDisconnectedInventory() =>
        Assert.DoesNotContain(RetainedGraphBuilder.Build(Graph(), includeTests: false).Entities, entity => entity.DisplayName == "orphan");

    [Fact] [Trait("Requirement", "PKG-03")]
    public void Build_RetainsReachableConfirmedRelationAndEvidence()
    {
        var result = RetainedGraphBuilder.Build(Graph(), includeTests: false);
        Assert.Equal("relation:root-terminal", Assert.Single(result.Relations).CanonicalKey);
        Assert.Equal("evidence:root", Assert.Single(result.Evidence).CanonicalKey);
    }

    [Fact] [Trait("Requirement", "PKG-03")]
    public void Build_RetainsOccurrencesSupportingAClosureEntity() =>
        Assert.Equal("entity:root", Assert.Single(RetainedGraphBuilder.Build(Graph(), includeTests: false).Entities.Where(x => x.DisplayName == "root")).CanonicalKey);

    [Fact] [Trait("Requirement", "PKG-09")]
    public void Build_OrdersRetainedFactsCanonically()
    {
        var result = RetainedGraphBuilder.Build(Graph(), includeTests: false);
        Assert.Equal(result.Entities.Select(x => x.CanonicalKey).Order(StringComparer.Ordinal), result.Entities.Select(x => x.CanonicalKey));
    }

    [Fact] [Trait("Requirement", "PKG-05")]
    public void Build_ExcludesARootOwnedOnlyByATestProjectByDefault()
    {
        var result = RetainedGraphBuilder.Build(Graph(rootProjectPath: "App.Tests/App.Tests.csproj"), includeTests: false);
        Assert.DoesNotContain(result.Entities, entity => entity.CanonicalKey == "entity:root");
    }

    [Fact] [Trait("Requirement", "PKG-08")]
    public void Build_IncludesATestProjectRootWhenPolicyEnabled()
    {
        var result = RetainedGraphBuilder.Build(Graph(rootProjectPath: "App.Tests/App.Tests.csproj"), includeTests: true);
        Assert.Contains(result.Entities, entity => entity.CanonicalKey == "entity:root");
    }

    [Fact] [Trait("Requirement", "EDG-01")]
    public void Build_RejectsRelationWithMissingSource() => Assert.Equal("invalid-confirmed-relation", Assert.Throws<RetentionException>(() => RetainedGraphBuilder.Build(Graph(source: "missing"), includeTests: false)).Cause);
    [Fact] [Trait("Requirement", "EDG-01")]
    public void Build_RejectsRelationWithMissingTarget() => Assert.Equal("invalid-confirmed-relation", Assert.Throws<RetentionException>(() => RetainedGraphBuilder.Build(Graph(target: "missing"), includeTests: false)).Cause);
    [Fact] [Trait("Requirement", "EDG-01")]
    public void Build_RejectsRelationWithoutEvidence() => Assert.Equal("invalid-confirmed-relation", Assert.Throws<RetentionException>(() => RetainedGraphBuilder.Build(Graph(evidenceKeys: []), includeTests: false)).Cause);
    [Fact] [Trait("Requirement", "EDG-01")]
    public void Build_RejectsRelationWithUnknownEvidence() => Assert.Equal("invalid-confirmed-relation", Assert.Throws<RetentionException>(() => RetainedGraphBuilder.Build(Graph(evidenceKeys: ["missing"]), includeTests: false)).Cause);

    private static FactualGraph Graph(EntityKind rootKind = EntityKind.Component, string source = "entity:root", string target = "entity:terminal", ImmutableArray<string>? evidenceKeys = null, string rootProjectPath = "App.csproj")
    {
        var solution = CanonicalIdentity.CreateSolution("app", "App.sln"); var project = CanonicalIdentity.CreateProject(solution, rootProjectPath); var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci"); var span = new SourceSpan(1, 1, 1, 2);
        var proof = new EvidenceRecord("evidence:root", "document:app", variant, span, "digest");
        return new FactualGraph(solution,
            [new LogicalEntity(rootKind, "entity:root", "root", null), new LogicalEntity(EntityKind.Symbol, "entity:terminal", "terminal", null), new LogicalEntity(EntityKind.Symbol, "entity:orphan", "orphan", null)],
            [new VariantOccurrence("entity:root", project, variant, new LogicalLocator("Root.cs", span, project), "shape", ["evidence:root"])],
            [proof], [new FactualRelation("relation:root-terminal", source, target, "internal-invocation", evidenceKeys ?? ["evidence:root"])], [], [], new ExtractionMeasurements(0, 0));
    }
}
