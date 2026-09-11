using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

/// <summary>
/// GCPC-038/GCPC-039/GCPC-042/GCPC-043 (fix for the F1 blocker): a compound fact family --
/// <c>facts/structural.json</c> and its siblings -- gets the same adaptive bucket-depth sharding
/// <see cref="LayoutPlannerShardingTests"/> already proves for flat record-array families, and a family
/// that splits still round-trips through <see cref="FactualPackageReader"/> to the same facts as its
/// unsplit equivalent.
/// </summary>
public sealed class CompoundFamilyShardingTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void Plan_OverCeilingStructuralFamily_IsSplitAndEveryShardFitsTheCeiling()
    {
        const int ceilingBytes = 512;
        var document = DomainMapper.ToWire(ManyProjectsSnapshot(count: 60), Context);

        var plan = LayoutPlanner.Plan(document, ceilingBytes);

        var shards = plan.Artifacts.Where(a => a.ArtifactKey.StartsWith("facts/structural", StringComparison.Ordinal)).ToArray();
        Assert.True(shards.Length > 1, "Expected the oversized compound family to split into more than one shard.");
        foreach (var shard in shards)
        {
            Assert.True(
                shard.PrecomputedBytes.Length <= ceilingBytes || shard.Records.Length == 1,
                $"Shard '{shard.ArtifactKey}' is {shard.PrecomputedBytes.Length} bytes, over the {ceilingBytes}-byte "
                + "ceiling, and holds more than one record.");
        }
    }

    [Fact]
    public void Plan_StructuralFamily_TwoRunsAssignEveryRecordToTheSameShard()
    {
        const int ceilingBytes = 512;
        var document = DomainMapper.ToWire(ManyProjectsSnapshot(count: 60), Context);

        var first = LayoutPlanner.Plan(document, ceilingBytes);
        var second = LayoutPlanner.Plan(document, ceilingBytes);

        foreach (var project in document.Projects)
        {
            Assert.True(first.FactLocations.TryGetValue(project.Identity.Id, out var firstCitation));
            Assert.True(second.FactLocations.TryGetValue(project.Identity.Id, out var secondCitation));
            Assert.Equal(firstCitation.ArtifactKey, secondCitation.ArtifactKey);
            Assert.Equal(firstCitation.Ordinal, secondCitation.Ordinal);
        }
    }

    [Fact]
    public void Plan_NoStructuralArtifactExceedsCeiling_NeverNamesADirectoryAfterAFactIdentity()
    {
        const int ceilingBytes = 512;
        var document = DomainMapper.ToWire(ManyProjectsSnapshot(count: 60), Context);

        var plan = LayoutPlanner.Plan(document, ceilingBytes);

        foreach (var artifact in plan.Artifacts.Where(a => a.ArtifactKey.StartsWith("facts/structural", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain('/', artifact.ArtifactKey["facts/".Length..]);
        }
    }

    [Fact]
    public void Publish_SplitStructuralFamily_RoundTripsThroughFactualPackageReaderToTheSameFactsAsUnsplit()
    {
        // PublicationPipeline.Publish always enforces the derived default ~32 KiB ceiling (T52), not
        // whatever ceiling the projector was constructed with -- so this needs enough Project facts to
        // genuinely exceed that real default, the same way ScaleInputGeneratorTests does for flat
        // families, rather than a tiny explicit test ceiling.
        var ceilingBytes = CeilingCalculator.Derive().CeilingBytes;
        var snapshot = ManyProjectsSnapshot(count: 500);
        var unsplitDocument = DomainMapper.ToWire(snapshot, Context);
        var unsplitProjectIds = unsplitDocument.Projects.Select(static p => p.Identity.Id).OrderBy(static id => id, StringComparer.Ordinal).ToArray();

        var tree = Directory.CreateTempSubdirectory("csharp2md-compound-shard-roundtrip-");
        try
        {
            var store = new FilesystemTransactionalStore(tree.FullName, new PackageProjector(ceilingBytes));
            var session = store.Open("src/Acme.sln", EmptySourceDocumentReader.Instance);
            session.Stage(snapshot);
            session.Commit();
            var packageDirectory = Directory.GetDirectories(tree.FullName).Single();

            var structuralShardFiles = Directory.EnumerateFiles(
                    Path.Combine(packageDirectory, "facts"), "structural.*.json", SearchOption.TopDirectoryOnly)
                .ToArray();
            Assert.True(
                structuralShardFiles.Length > 1,
                "Expected the fixture to actually split facts/structural.json for this test to be meaningful.");
            foreach (var file in structuralShardFiles)
            {
                Assert.True(
                    new FileInfo(file).Length <= ceilingBytes,
                    $"'{file}' exceeds the {ceilingBytes}-byte ceiling.");
            }

            var result = FactualPackageReader.Read(packageDirectory);
            var rehydratedProjectIds = result.Snapshot.Facts
                .OfType<Project>()
                .Select(static p => p.Reference.Id.Value)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(unsplitProjectIds, rehydratedProjectIds);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static FactualSnapshot ManyProjectsSnapshot(int count)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var solution = Solution.Create(solutionId);

        var facts = new List<IFact> { solution };
        for (var i = 0; i < count; i++)
        {
            var projectId = ProjectId.Create(solutionId, $"src/Acme.Project{i:D3}/Acme.Project{i:D3}.csproj");
            facts.Add(Project.Create(projectId));
        }

        return new FactualSnapshot([.. facts], [], [], [], [], []);
    }
}
