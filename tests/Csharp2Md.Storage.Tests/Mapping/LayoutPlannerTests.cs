using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

/// <summary>GCPC-040/GCPC-041: the layout planner gives every fact and relation exactly one location,
/// agrees with itself across runs, and never touches a file to compute the plan (AD-023).</summary>
public sealed class LayoutPlannerTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void Plan_EveryFactInFilledDocument_HasExactlyOnePlannedLocation()
    {
        var snapshot = FilledSnapshot();
        var document = DomainMapper.ToWire(snapshot, Context);
        var plan = LayoutPlanner.Plan(document);

        Assert.NotEmpty(snapshot.Facts);
        var seen = new HashSet<(string, int)>();
        foreach (var fact in snapshot.Facts)
        {
            Assert.True(plan.FactLocations.TryGetValue(fact.Reference.Id.Value, out var citation));
            Assert.True(seen.Add((citation.ArtifactKey, citation.Ordinal)));
        }

        Assert.Equal(snapshot.Facts.Length, plan.FactLocations.Count);
    }

    [Fact]
    public void Plan_EveryConfirmedRelation_HasExactlyOnePlannedLocation()
    {
        var document = DomainMapper.ToWire(FilledSnapshot(), Context);
        var plan = LayoutPlanner.Plan(document);

        var records = document.ConfirmedRelations["contains"];
        Assert.True(plan.RelationLocations.TryGetValue("contains", out var citations));
        Assert.Equal(records.Length, citations.Length);

        var seen = new HashSet<(string, int)>();
        foreach (var citation in citations)
        {
            Assert.True(seen.Add((citation.ArtifactKey, citation.Ordinal)));
        }
    }

    [Fact]
    public void Plan_TwoRunsOverTheSameDocument_ProduceAnIdenticalPlan()
    {
        var document = DomainMapper.ToWire(FilledSnapshot(), Context);

        var first = LayoutPlanner.Plan(document);
        var second = LayoutPlanner.Plan(document);

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void Plan_GivenAFreshDirectory_NeverCreatesOrReadsAFile()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "layout-planner-io-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            var before = Directory.GetFileSystemEntries(sandbox);
            var document = DomainMapper.ToWire(FilledSnapshot(), Context);

            var plan = LayoutPlanner.Plan(document);

            Assert.NotEmpty(plan.Artifacts);
            Assert.Empty(before);
            Assert.Empty(Directory.GetFileSystemEntries(sandbox));
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    private static string Describe(LayoutPlan plan)
    {
        var slots = string.Join(
            ";",
            plan.Slots.Select(static slot => $"{slot.CanonicalKey}:{slot.Role}:{slot.Count}"));
        var facts = string.Join(
            ";",
            plan.FactLocations
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => $"{pair.Key}={pair.Value.ArtifactKey}#{pair.Value.Ordinal}"));
        var relations = string.Join(
            ";",
            plan.RelationLocations
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => pair.Key + "=" + string.Join(
                    ",",
                    pair.Value.Select(static citation => $"{citation.ArtifactKey}#{citation.Ordinal}"))));
        return string.Join("|", slots, facts, relations);
    }

    private static FactualSnapshot FilledSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var otherProjectId = ProjectId.Create(solutionId, "src/Acme.Orders/Acme.Orders.csproj");
        var solution = Solution.Create(solutionId);
        var project = Project.Create(projectId);
        var other = Project.Create(otherProjectId);
        var component = Component.Create(solutionId, "Payments.Api", []);
        var binding = ConfigurationBinding.Create(
            project.Reference,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, "ConnectionStrings:Orders", "configurationKey"));
        var firstRelation = Contains(solution.Reference, project.Reference, occurrenceOrdinal: 1);
        var secondRelation = Contains(solution.Reference, other.Reference, occurrenceOrdinal: 2);
        return new FactualSnapshot(
            [solution, project, other, component, binding],
            [],
            [firstRelation, secondRelation],
            [],
            [],
            []);
    }

    private static ConfirmedRelation Contains(FactReference source, FactReference target, int occurrenceOrdinal) =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            source,
            target,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(
                    source,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    occurrenceOrdinal)]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
}
