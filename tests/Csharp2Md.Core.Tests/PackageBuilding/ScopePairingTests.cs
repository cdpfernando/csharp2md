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

    private static ImmutableArray<AggregatedDependency> Dependencies(FactualGraph graph)
    {
        var plan = PackageBuilder.Build([graph]);
        var artifacts = plan.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        return Assert.Single(RetrievalModelReader.Read(artifacts).Solutions).Dependencies;
    }

    private static FactualGraph Graph(bool includeEvidenceDocumentOccurrence = true)
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
                new SourceDocumentSnapshot(EvidenceDocument, new LogicalLocator("Caller.cs", span, project), false, "a"),
                new SourceDocumentSnapshot(SilentDocument, new LogicalLocator("Other.cs", span, project), false, "b"),
                new SourceDocumentSnapshot(TargetDocument, new LogicalLocator("Target.cs", span, project), false, "c"),
                new SourceDocumentSnapshot(ComponentDocument, new LogicalLocator("App.csproj", span, project), false, "d"),
            ],
            new ExtractionMeasurements(0, 0));

        VariantOccurrence Occurrence(string entity, string path, string evidenceKey) =>
            new(entity, project, variant, new LogicalLocator(path, span, project), "shape:" + entity, [evidenceKey]);
    }
}
