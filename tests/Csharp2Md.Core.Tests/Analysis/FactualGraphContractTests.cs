using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class FactualGraphContractTests
{
    [Fact]
    [Trait("Requirement", "VAR-03")]
    public void LogicalEntity_IsIdentifiedIndependentlyOfVariantOccurrences()
    {
        var entity = Entity("component:orders");
        var net8 = Occurrence("component:orders", Variant("net8.0"), "src/Orders/Orders.cs");
        var net10 = Occurrence("component:orders", Variant("net10.0"), "src/Orders/Orders.cs");

        var graph = Graph(entities: [entity], occurrences: [net8, net10]);

        Assert.Equal("component:orders", Assert.Single(graph.Entities).CanonicalKey);
        Assert.Equal(2, graph.Occurrences.Length);
        Assert.All(graph.Occurrences, occurrence => Assert.Equal("component:orders", occurrence.EntityCanonicalKey));
        Assert.NotEqual(graph.Occurrences[0].Variant, graph.Occurrences[1].Variant);
    }

    [Fact]
    [Trait("Requirement", "VAR-04")]
    public void OccurrenceAndEvidence_DeclareTheAnalysisVariantThatProducedThem()
    {
        var variant = Variant("net10.0");
        var occurrence = Occurrence("symbol:run", variant, "src/Orders/Orders.cs", evidenceKeys: ["ev-1"]);
        var evidence = new EvidenceRecord(
            "ev-1",
            "doc:orders",
            variant,
            new SourceSpan(1, 1, 2, 1),
            "digest");

        Assert.Equal(variant, occurrence.Variant);
        Assert.Equal(variant, evidence.Variant);
        Assert.Equal("src/Orders/Orders.cs", occurrence.Locator.RelativePath);
    }

    [Theory]
    [Trait("Requirement", "PKG-07")]
    [InlineData("/repo/src/Orders.cs")]
    [InlineData("C:/repo/src/Orders.cs")]
    [InlineData("src\\Orders.cs")]
    [InlineData("src/../Orders.cs")]
    [InlineData("src/./Orders.cs")]
    public void LogicalLocator_RootedEscapingOrNonNormalizedPath_IsRejected(string path)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new LogicalLocator(path, new SourceSpan(1, 1, 1, 2), Project()));

        Assert.Equal("relativePath", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void LogicalLocator_AcceptsForwardSlashRelativePath()
    {
        var locator = new LogicalLocator("src/Orders/Orders.cs", new SourceSpan(3, 1, 8, 2), Project());

        Assert.Equal("src/Orders/Orders.cs", locator.RelativePath);
        Assert.DoesNotContain(":", locator.RelativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", locator.RelativePath, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "PKG-03")]
    public void FactualGraph_DefaultCollections_AreOwnedEmptyArrays()
    {
        var graph = new FactualGraph(
            new SolutionIdentity("solution:acme", "src/Acme.sln"),
            default,
            default,
            default,
            default,
            default,
            default,
            new ExtractionMeasurements(0, 0));

        Assert.False(graph.Entities.IsDefault);
        Assert.Empty(graph.Entities);
        Assert.Empty(graph.Occurrences);
        Assert.Empty(graph.Evidence);
        Assert.Empty(graph.Relations);
        Assert.Empty(graph.Gaps);
        Assert.Empty(graph.Sources);
    }

    [Fact]
    [Trait("Requirement", "PKG-03")]
    public void FactualGraph_DoesNotShareMutableBuilderStorage()
    {
        var entities = ImmutableArray.CreateBuilder<LogicalEntity>();
        entities.Add(Entity("component:orders"));
        var snapshot = entities.ToImmutable();
        var graph = Graph(entities: snapshot);
        entities.Add(Entity("component:billing"));

        Assert.Equal("component:orders", Assert.Single(graph.Entities).CanonicalKey);
        Assert.Equal(2, entities.Count);
    }

    [Fact]
    [Trait("Requirement", "PKG-04")]
    public void KnowledgeGap_UsesCandidateUnknownOrOpenFrontierKindsOnly()
    {
        var kinds = Enum.GetValues<GapKind>();

        Assert.Equal(
            [GapKind.Candidate, GapKind.Unknown, GapKind.OpenFrontier],
            kinds);
        var gap = new KnowledgeGap(
            "gap:unknown-target",
            GapKind.Unknown,
            "unresolved-destination",
            ImmutableArray.Create("symbol:run"),
            ImmutableArray.Create("ev-1"));
        Assert.Equal(GapKind.Unknown, gap.Kind);
        Assert.Equal("unresolved-destination", gap.Cause);
    }

    [Fact]
    [Trait("Requirement", "PKG-09")]
    public void FactualGraphTypes_DoNotExposeBusinessRuleOrQualityInterpretation()
    {
        var forbidden = typeof(FactualGraph).Assembly.GetTypes()
            .Where(type => type.Namespace == "Csharp2Md.Core.Analysis")
            .SelectMany(type => type.GetProperties())
            .Where(property => property.Name.Contains("Score", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Quality", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Risk", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToArray();

        Assert.True(forbidden.Length == 0, "Factual models exposed interpretation or secret fields: " + string.Join(", ", forbidden));
    }

    [Fact]
    [Trait("Requirement", "PKG-02")]
    public void EntityKind_IncludesProvenArchitecturalRoots()
    {
        Assert.Contains(EntityKind.Component, Enum.GetValues<EntityKind>());
        Assert.Contains(EntityKind.DeploymentUnit, Enum.GetValues<EntityKind>());
        Assert.Contains(EntityKind.EntryPoint, Enum.GetValues<EntityKind>());
        Assert.Contains(EntityKind.BoundaryOperation, Enum.GetValues<EntityKind>());
        Assert.Equal(EntityKind.Component, Entity("component:orders", EntityKind.Component).Kind);
    }

    private static FactualGraph Graph(
        ImmutableArray<LogicalEntity>? entities = null,
        ImmutableArray<VariantOccurrence>? occurrences = null) =>
        new(
            new SolutionIdentity("solution:acme", "src/Acme.sln"),
            entities ?? ImmutableArray<LogicalEntity>.Empty,
            occurrences ?? ImmutableArray<VariantOccurrence>.Empty,
            ImmutableArray<EvidenceRecord>.Empty,
            ImmutableArray<FactualRelation>.Empty,
            ImmutableArray<KnowledgeGap>.Empty,
            ImmutableArray<SourceDocumentSnapshot>.Empty,
            new ExtractionMeasurements(0, 0));

    private static LogicalEntity Entity(string key, EntityKind kind = EntityKind.Component) =>
        new(kind, key, "Orders", "Acme.Orders");

    private static VariantOccurrence Occurrence(
        string entityKey,
        AnalysisVariant variant,
        string path,
        ImmutableArray<string>? evidenceKeys = null) =>
        new(
            entityKey,
            Project(),
            variant,
            new LogicalLocator(path, new SourceSpan(1, 1, 1, 8), Project()),
            "shape",
            evidenceKeys ?? ImmutableArray<string>.Empty);

    private static AnalysisVariant Variant(string tfm) =>
        new(tfm, "Release", ImmutableArray.Create("TRACE"), "ci");

    private static ProjectIdentity Project() =>
        new("project:orders", "src/Orders/Orders.csproj");
}
