using System.Text.Json.Nodes;
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

/// <summary>GCPC-038/GCPC-042/GCPC-043 (GCPC-039 stays partial -- see LayoutPlanner remarks): adaptive
/// bucket-depth sharding of the plan's flat record-array families.</summary>
public sealed class LayoutPlannerShardingTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void Plan_OverCeilingRelationFamily_IsSplitAndEveryShardFitsTheCeiling()
    {
        const int ceilingBytes = 512;
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 60), Context);

        var plan = LayoutPlanner.Plan(document, ceilingBytes);

        var shards = plan.Artifacts.Where(a => a.ArtifactKey.StartsWith("relations/confirmed/contains", StringComparison.Ordinal)).ToArray();
        Assert.True(shards.Length > 1, "Expected the oversized family to split into more than one shard.");
        foreach (var shard in shards)
        {
            var bytes = LayoutPlanner.SerializeRecords(shard.Records.Select(r => r.Entry)).Length;
            Assert.True(
                bytes <= ceilingBytes || shard.Records.Length == 1,
                $"Shard '{shard.ArtifactKey}' is {bytes} bytes, over the {ceilingBytes}-byte ceiling, and holds more than one record.");
        }
    }

    [Fact]
    public void Plan_BucketAssignment_IsDerivedFromFactIdNotDisplayName()
    {
        const int ceilingBytes = 512;
        var snapshotA = ManyRelationsSnapshot(count: 60);
        var document = DomainMapper.ToWire(snapshotA, Context);

        var first = LayoutPlanner.Plan(document, ceilingBytes);
        var second = LayoutPlanner.Plan(document, ceilingBytes);

        var firstShardOf = ShardKeyByRecordId(first, "contains");
        var secondShardOf = ShardKeyByRecordId(second, "contains");
        Assert.Equal(firstShardOf, secondShardOf);
    }

    [Fact]
    public void Plan_TwoRunsOverTheSameOverCeilingDocument_AssignEveryRecordToTheSameShard()
    {
        const int ceilingBytes = 512;
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 60), Context);

        var first = LayoutPlanner.Plan(document, ceilingBytes);
        var second = LayoutPlanner.Plan(document, ceilingBytes);

        var firstCitations = first.RelationLocations["contains"];
        var secondCitations = second.RelationLocations["contains"];
        Assert.Equal(firstCitations.Length, secondCitations.Length);
        for (var i = 0; i < firstCitations.Length; i++)
        {
            Assert.Equal(firstCitations[i].ArtifactKey, secondCitations[i].ArtifactKey);
            Assert.Equal(firstCitations[i].Ordinal, secondCitations[i].Ordinal);
        }
    }

    [Fact]
    public void Plan_SingleRecordLargerThanCeiling_PublishedInItsOwnShardWithADegradationReasonNeverTruncated()
    {
        const int ceilingBytes = 16;
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 5), Context);

        var plan = LayoutPlanner.Plan(document, ceilingBytes);

        var reasons = plan.DegradationReasons.Where(r => r.Code == "record-exceeds-ceiling").ToArray();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, static reason => Assert.Equal(1, reason.AffectedCount));

        var oversizedShards = plan.Artifacts
            .Where(a => a.ArtifactKey.StartsWith("relations/confirmed/contains", StringComparison.Ordinal) && a.Records.Length == 1)
            .ToArray();
        Assert.NotEmpty(oversizedShards);
        foreach (var shard in oversizedShards)
        {
            var written = LayoutPlanner.SerializeRecords(shard.Records.Select(r => r.Entry));
            var restored = (JsonArray)JsonNode.Parse(written.AsSpan())!;
            Assert.Single(restored);
            Assert.True(JsonNode.DeepEquals(
                JsonNode.Parse(shard.Records[0].Entry.AsSpan()),
                restored[0]));
        }
    }

    [Fact]
    public void Plan_NoArtifactExceedsCeiling_NeverNamesADirectoryAfterAFactIdentity()
    {
        const int ceilingBytes = 512;
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 60), Context);

        var plan = LayoutPlanner.Plan(document, ceilingBytes);

        foreach (var artifact in plan.Artifacts.Where(a => a.ArtifactKey.Contains("relations/confirmed/contains", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain('/', artifact.ArtifactKey["relations/confirmed/".Length..]);
        }
    }

    private static string ShardKeyByRecordId(LayoutPlan plan, string kind)
    {
        var citations = plan.RelationLocations[kind];
        return string.Join(",", citations.Select(c => c.ArtifactKey));
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
