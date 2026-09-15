using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

/// <summary>GCPC-041: a citation resolves to the record it claims whether or not its family was split.</summary>
public sealed class PublishedPackageViewShardAwareTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void TryLocateRelation_UnsplitPayload_CitationResolvesToTheClaimedRecord()
    {
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 5), Context);
        var view = PublishedPackageView.From(document, LayoutPlanner.Plan(document, int.MaxValue));

        var records = document.ConfirmedRelations["contains"];
        for (var index = 0; index < records.Length; index++)
        {
            Assert.True(view.TryLocateRelation("contains", index, out var citation));
            var artifact = Assert.Single(view.Plan.Artifacts, a => a.ArtifactKey == citation.ArtifactKey);
            var expectedIdentity = $"contains:{records[index].Source.Id}:{records[index].Target.Id}";
            Assert.Equal(expectedIdentity, artifact.Records[citation.Ordinal].Identity);
        }
    }

    [Fact]
    public void TryLocateRelation_SplitPayload_EveryCitationResolvesToTheClaimedRecord()
    {
        const int ceilingBytes = 512;
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 60), Context);
        var plan = LayoutPlanner.Plan(document, ceilingBytes);
        var view = PublishedPackageView.From(document, plan);

        var shardKeys = plan.Artifacts
            .Where(a => a.ArtifactKey.StartsWith("relations/confirmed/contains", StringComparison.Ordinal))
            .Select(a => a.ArtifactKey)
            .Distinct()
            .ToArray();
        Assert.True(shardKeys.Length > 1, "Expected the fixture to actually split for this test to be meaningful.");

        var records = document.ConfirmedRelations["contains"];
        for (var index = 0; index < records.Length; index++)
        {
            Assert.True(view.TryLocateRelation("contains", index, out var citation));
            var artifact = Assert.Single(view.Plan.Artifacts, a => a.ArtifactKey == citation.ArtifactKey);
            var expectedIdentity = $"contains:{records[index].Source.Id}:{records[index].Target.Id}";
            Assert.Equal(expectedIdentity, artifact.Records[citation.Ordinal].Identity);
        }
    }

    [Fact]
    public void TryLocateRelation_UnknownKindOrOutOfRangeIndex_ReturnsFalse()
    {
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 3), Context);
        var view = PublishedPackageView.From(document);

        Assert.False(view.TryLocateRelation("invokes", 0, out _));
        Assert.False(view.TryLocateRelation("contains", -1, out _));
        Assert.False(view.TryLocateRelation("contains", 999, out _));
    }

    private static FactualSnapshot ManyRelationsSnapshot(int count)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var solution = Solution.Create(solutionId);

        var facts = new List<IFact> { solution };
        var relations = new List<ConfirmedRelation>();
        for (var i = 0; i < count; i++)
        {
            var projectId = ProjectId.Create(solutionId, $"src/Acme.Project{i:D3}/Acme.Project{i:D3}.csproj");
            var project = Project.Create(projectId);
            facts.Add(project);
            relations.Add(Contains(solution.Reference, project.Reference, occurrenceOrdinal: i + 1));
        }

        return new FactualSnapshot([.. facts], [], [.. relations], [], [], []);
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
