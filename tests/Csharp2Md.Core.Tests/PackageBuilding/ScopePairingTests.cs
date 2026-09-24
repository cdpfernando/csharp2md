using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Measures;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class ScopePairingTests
{
    private static readonly SolutionIdentity Solution = CanonicalIdentity.CreateSolution("app", "App.sln");
    private static readonly string EvidenceDocument = CanonicalIdentity.CreateDocumentKey(Solution, "Caller.cs");
    private static readonly string SilentDocument = CanonicalIdentity.CreateDocumentKey(Solution, "Other.cs");
    private static readonly string TargetDocument = CanonicalIdentity.CreateDocumentKey(Solution, "Target.cs");
    private static readonly string ComponentDocument = CanonicalIdentity.CreateDocumentKey(Solution, "App.csproj");
    private const string TestProjectPath = "App.Tests/App.Tests.csproj";
    private static readonly string TestDocument = CanonicalIdentity.CreateDocumentKey(Solution, "App.Tests/TargetCalledFromTest.cs");

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_DocumentEdgeStartsAtTheDocumentHoldingTheEvidence() =>
        Assert.Equal(EvidenceDocument, Assert.Single(EdgesInto(TargetDocument)).Source.Value);

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_DocumentEdgeOmitsASourceDocumentNoEvidencePlacesThere() =>
        Assert.DoesNotContain(Edges(AggregationScope.Document), edge => edge.Source.Value == SilentDocument);

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_DocumentEdgeKeepsTheTargetMembershipDocument() =>
        Assert.Contains(Edges(AggregationScope.Document), edge => edge.Target.Value == TargetDocument);

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_ProjectEdgeStartsAtTheProjectOwningTheEvidenceDocument() =>
        Assert.Equal(
            CanonicalIdentity.CreateProject(Solution, "App.csproj").CanonicalKey,
            Assert.Single(Edges(AggregationScope.Project)).Source.Value);

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_ProjectEdgeTargetsTheProjectContainingTheTarget() =>
        Assert.Equal(
            CanonicalIdentity.CreateProject(Solution, "App.csproj").CanonicalKey,
            Assert.Single(Edges(AggregationScope.Project)).Target.Value);

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_ProjectEdgeIsAbsentWhenEvidenceDocumentHasNoProvenOwner() =>
        Assert.DoesNotContain(
            Dependencies(Graph(includeEvidenceDocumentOccurrence: false)),
            edge => edge.Scope == AggregationScope.Project
                && edge.Relations.Any(relation => relation.Value == "relation:source-target"));

    [Fact] [Trait("Requirement", "DEP-04")]
    public void Build_OccurrenceCountEqualsTheConfirmedEvidenceCount() =>
        Assert.Equal(1, Assert.Single(EdgesInto(TargetDocument)).OccurrenceCount);

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_ComponentScopeKeepsItsMembershipDerivation() =>
        Assert.Contains(Edges(AggregationScope.Component), edge =>
            edge.Source.Value == "entity:component" && edge.Target.Value == "entity:component");

    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_DeploymentUnitScopeKeepsItsMembershipDerivation() =>
        Assert.Contains(Edges(AggregationScope.DeploymentUnit), edge =>
            edge.Source.Value == "entity:deployment" && edge.Target.Value == "entity:deployment");

    // The two cases above are single-component fixtures, so a self-edge is the only shape they can ever
    // produce - it cannot tell a correct Component-scope projection from a defective one that fans every
    // relation out to every component regardless of whose occurrences it actually touches (the eShop
    // fan-out this project measured: 138 of 144 possible pairs before the fix). This fixture has two real
    // components with a relation whose endpoints are attributable one to each, so the pairing must name
    // exactly that pair - not the two components' own self-edges instead of, or in addition to, it.
    [Fact] [Trait("Requirement", "DEP-01")]
    public void Build_ComponentScopeAggregatesAGenuineCrossComponentRelation()
    {
        var edges = Dependencies(TwoComponentGraph()).Where(edge => edge.Scope == AggregationScope.Component).ToArray();

        Assert.Contains(edges, edge => edge.Source.Value == "entity:component-a" && edge.Target.Value == "entity:component-b");
        Assert.DoesNotContain(edges, edge => edge.Source.Value == "entity:component-a" && edge.Target.Value == "entity:component-a");
        Assert.DoesNotContain(edges, edge => edge.Source.Value == "entity:component-b" && edge.Target.Value == "entity:component-b");
    }

    // PKG-05: a genuinely-retained target entity (called from real production code) that is ALSO called
    // from a test project must not have its Document/Project membership widened by that test-side
    // occurrence - otherwise a real dependency edge fans out to a test file/project it has nothing to do
    // with, purely because the target symbol happens to be exercised by a test too.
    [Fact] [Trait("Requirement", "PKG-05")]
    public void Build_TargetMembershipExcludesATestProjectOccurrenceByDefault()
    {
        var edges = Dependencies(Graph(includeTestOccurrenceOnTarget: true));
        Assert.DoesNotContain(edges, edge => edge.Scope == AggregationScope.Document && edge.Target.Value == TestDocument);
        Assert.DoesNotContain(edges, edge => edge.Scope == AggregationScope.Project && edge.Target.Value == CanonicalIdentity.CreateProject(Solution, TestProjectPath).CanonicalKey);
    }

    [Fact] [Trait("Requirement", "PKG-08")]
    public void Build_TargetMembershipIncludesATestProjectOccurrenceWhenPolicyEnabled()
    {
        var edges = Dependencies(Graph(includeTestOccurrenceOnTarget: true), includeTests: true);
        Assert.Contains(edges, edge => edge.Scope == AggregationScope.Document && edge.Target.Value == TestDocument);
    }


    // DEP-05 is about one low-level relation contributing to MORE THAN ONE scope: its reference is reused and
    // its factual payload is not duplicated. Repetition inside a single scope does not exercise that.
    // `relation:source-target` is confirmed once in Caller.cs and reaches Document, Project, Component and
    // Deployment Unit through membership, so it is the case the criterion describes.
    [Fact] [Trait("Requirement", "DEP-05")]
    public void Build_ReusesOneRelationAcrossEveryScopeItContributesTo()
    {
        var carrying = Dependencies()
            .Where(edge => edge.Relations.Any(relation => relation.Value == "relation:source-target"))
            .ToArray();
        Assert.Equal(
            [AggregationScope.Document, AggregationScope.Project, AggregationScope.Component, AggregationScope.DeploymentUnit],
            carrying.Select(edge => edge.Scope).Distinct().Order());
        Assert.All(carrying, edge => Assert.Single(edge.Relations, relation => relation.Value == "relation:source-target"));
    }

    // The payload half of DEP-05. The relation's factual record is written once into the shard's `relations`
    // table and every scope row points at it by ordinal, so the canonical key occurs exactly once in the whole
    // package however many scopes carry the edge.
    [Fact] [Trait("Requirement", "DEP-05")]
    public void Build_WritesTheSharedRelationPayloadOnlyOnce()
    {
        var plan = PackageBuilder.Build([Graph()]);
        var occurrences = plan.Artifacts.Sum(artifact =>
            Occurrences(Encoding.UTF8.GetString(artifact.Payload.AsSpan()), "relation:source-target"));
        Assert.Equal(1, occurrences);
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static ImmutableArray<AggregatedDependency> EdgesInto(string document) =>
        Edges(AggregationScope.Document).Where(edge => edge.Target.Value == document).ToImmutableArray();

    private static ImmutableArray<AggregatedDependency> Edges(AggregationScope scope) =>
        Dependencies().Where(edge => edge.Scope == scope).ToImmutableArray();

    private static ImmutableArray<AggregatedDependency> Dependencies()
        => Dependencies(Graph());

    private static ImmutableArray<AggregatedDependency> Dependencies(FactualGraph graph, bool includeTests = false)
    {
        var plan = PackageBuilder.Build([graph], includeTests);
        var artifacts = plan.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        return Assert.Single(RetrievalModelReader.Read(artifacts).Solutions).Dependencies;
    }

    private static FactualGraph Graph(bool includeEvidenceDocumentOccurrence = true, bool includeTestOccurrenceOnTarget = false)
    {
        var project = CanonicalIdentity.CreateProject(Solution, "App.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var span = new SourceSpan(1, 1, 1, 1);
        var proof = new EvidenceRecord("evidence:one", EvidenceDocument, variant, span, "digest");
        var componentProof = new EvidenceRecord("evidence:component", ComponentDocument, variant, span, "component-digest");

        var entities = new[]
        {
            new LogicalEntity(EntityKind.Component, "entity:component", "component", null),
            new LogicalEntity(EntityKind.DeploymentUnit, "entity:deployment", "deployment", null),
            new LogicalEntity(EntityKind.Symbol, "entity:source", "source", null),
            new LogicalEntity(EntityKind.Symbol, "entity:target", "target", null),
        };

        var occurrences = new List<VariantOccurrence>
        {
            Occurrence("entity:component", "App.csproj", "evidence:component"),
            Occurrence("entity:deployment", "App.csproj", "evidence:component"),
            Occurrence("entity:source", "Other.cs", "evidence:one"),
            Occurrence("entity:target", "Target.cs", "evidence:one"),
        };
        if (includeEvidenceDocumentOccurrence)
            occurrences.Add(Occurrence("entity:source", "Caller.cs", "evidence:one"));

        var sources = new List<SourceDocumentSnapshot>
        {
            new(EvidenceDocument, new LogicalLocator("Caller.cs", span, project), false, "a"),
            new(SilentDocument, new LogicalLocator("Other.cs", span, project), false, "b"),
            new(TargetDocument, new LogicalLocator("Target.cs", span, project), false, "c"),
            new(ComponentDocument, new LogicalLocator("App.csproj", span, project), false, "d"),
        };

        if (includeTestOccurrenceOnTarget)
        {
            var testProject = CanonicalIdentity.CreateProject(Solution, TestProjectPath);
            occurrences.Add(new VariantOccurrence("entity:target", testProject, variant, new LogicalLocator("App.Tests/TargetCalledFromTest.cs", span, testProject), "shape:entity:target", ["evidence:one"]));
            sources.Add(new SourceDocumentSnapshot(TestDocument, new LogicalLocator("App.Tests/TargetCalledFromTest.cs", span, testProject), true, "e"));
        }

        return new FactualGraph(
            Solution,
            [.. entities],
            [.. occurrences],
            [proof, componentProof],
            [
                new FactualRelation("relation:source-target", "entity:source", "entity:target", "internal-invocation", ["evidence:one"]),
                new FactualRelation("relation:component-source", "entity:component", "entity:source", "internal-invocation", ["evidence:component"]),
            ],
            [],
            [
                .. sources,
            ],
            new ExtractionMeasurements(0, 0));

        VariantOccurrence Occurrence(string entity, string path, string evidenceKey) =>
            new(entity, project, variant, new LogicalLocator(path, span, project), "shape:" + entity, [evidenceKey]);
    }

    private static FactualGraph TwoComponentGraph()
    {
        var projectA = CanonicalIdentity.CreateProject(Solution, "App.A.csproj");
        var projectB = CanonicalIdentity.CreateProject(Solution, "App.B.csproj");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var span = new SourceSpan(1, 1, 1, 1);
        var callProof = new EvidenceRecord("evidence:cross-call", CanonicalIdentity.CreateDocumentKey(Solution, "App.A/Caller.cs"), variant, span, "cross-digest");
        var componentAProof = new EvidenceRecord("evidence:component-a", CanonicalIdentity.CreateDocumentKey(Solution, "App.A.csproj"), variant, span, "component-a-digest");
        var componentBProof = new EvidenceRecord("evidence:component-b", CanonicalIdentity.CreateDocumentKey(Solution, "App.B.csproj"), variant, span, "component-b-digest");

        var entities = new[]
        {
            new LogicalEntity(EntityKind.Component, "entity:component-a", "component-a", null),
            new LogicalEntity(EntityKind.Component, "entity:component-b", "component-b", null),
            new LogicalEntity(EntityKind.Symbol, "entity:cross-source", "cross-source", null),
            new LogicalEntity(EntityKind.Symbol, "entity:cross-target", "cross-target", null),
        };

        var occurrences = new[]
        {
            new VariantOccurrence("entity:component-a", projectA, variant, new LogicalLocator("App.A.csproj", span, projectA), "shape:entity:component-a", ["evidence:component-a"]),
            new VariantOccurrence("entity:component-b", projectB, variant, new LogicalLocator("App.B.csproj", span, projectB), "shape:entity:component-b", ["evidence:component-b"]),
            new VariantOccurrence("entity:cross-source", projectA, variant, new LogicalLocator("App.A/Caller.cs", span, projectA), "shape:entity:cross-source", ["evidence:cross-call"]),
            new VariantOccurrence("entity:cross-target", projectB, variant, new LogicalLocator("App.B/Callee.cs", span, projectB), "shape:entity:cross-target", ["evidence:cross-call"]),
        };

        var sources = new[]
        {
            new SourceDocumentSnapshot(CanonicalIdentity.CreateDocumentKey(Solution, "App.A/Caller.cs"), new LogicalLocator("App.A/Caller.cs", span, projectA), false, "a"),
            new SourceDocumentSnapshot(CanonicalIdentity.CreateDocumentKey(Solution, "App.B/Callee.cs"), new LogicalLocator("App.B/Callee.cs", span, projectB), false, "b"),
            new SourceDocumentSnapshot(CanonicalIdentity.CreateDocumentKey(Solution, "App.A.csproj"), new LogicalLocator("App.A.csproj", span, projectA), false, "c"),
            new SourceDocumentSnapshot(CanonicalIdentity.CreateDocumentKey(Solution, "App.B.csproj"), new LogicalLocator("App.B.csproj", span, projectB), false, "d"),
        };

        return new FactualGraph(
            Solution,
            [.. entities],
            [.. occurrences],
            [callProof, componentAProof, componentBProof],
            [new FactualRelation("relation:cross-component", "entity:cross-source", "entity:cross-target", "internal-invocation", ["evidence:cross-call"])],
            [],
            [.. sources],
            new ExtractionMeasurements(0, 0));
    }
}
