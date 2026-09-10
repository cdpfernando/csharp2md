using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Reading;

/// <summary>GCPC-064 (partial -- the `validate` CLI verb itself is Phase 9): the reader enumerates the
/// manifest, merges a split family back into one array, opens nothing the manifest does not list, and
/// returns projection fragments alongside the document.</summary>
[Collection(FilesystemStoreCollection.Name)]
public sealed class ManifestDrivenReaderTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void Read_ShardedPackage_ReadsBackToTheSameDocumentAsItsUnshardedEquivalent()
    {
        using var output = TempOutputRoot.Create();
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 60), Context);

        var unshardedPath = Path.Combine(output.DirectoryPath, "unsharded");
        PackageDirectoryWriter.Write(unshardedPath, document, LayoutPlanner.Plan(document, int.MaxValue));

        var shardedPath = Path.Combine(output.DirectoryPath, "sharded");
        var shardedPlan = LayoutPlanner.Plan(document, ceilingBytes: 512);
        PackageDirectoryWriter.Write(shardedPath, document, shardedPlan);
        Assert.True(shardedPlan.Artifacts.Count(a => a.ArtifactKey.StartsWith("relations/confirmed/contains", StringComparison.Ordinal)) > 1);

        var unsharded = FactualPackageReader.Read(unshardedPath);
        var sharded = FactualPackageReader.Read(shardedPath);

        var unshardedIds = ContainsTargetIds(unsharded.Snapshot);
        var shardedIds = ContainsTargetIds(sharded.Snapshot);
        Assert.NotEmpty(unshardedIds);
        Assert.Equal(
            unshardedIds.OrderBy(static id => id, StringComparer.Ordinal),
            shardedIds.OrderBy(static id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void Read_PackageWithAnUndeclaredStrayFile_NeverOpensIt()
    {
        using var output = TempOutputRoot.Create();
        var package = Path.Combine(output.DirectoryPath, "package-with-stray-file");
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 3), Context);
        PackageDirectoryWriter.Write(package, document, LayoutPlanner.Plan(document, int.MaxValue));

        var strayPath = Path.Combine(package, "stray-not-in-manifest.json");
        File.WriteAllText(strayPath, "{ this is not valid JSON and would throw if ever parsed");

        var result = FactualPackageReader.Read(package);

        Assert.NotEmpty(result.Snapshot.Facts);
        Assert.DoesNotContain(result.Projections, fragment => fragment.CanonicalKey.Contains("stray", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_PackageWithAProjectionArtifact_ReturnsItAlongsideTheDocument()
    {
        using var output = TempOutputRoot.Create();
        var package = Path.Combine(output.DirectoryPath, "package-with-projection");
        var document = DomainMapper.ToWire(ManyRelationsSnapshot(count: 3), Context);
        var plan = LayoutPlanner.Plan(document, int.MaxValue);
        var projectionBytes = CanonicalJson.Write(document.Solutions);
        var projections = ImmutableArray.Create(
            new StagedFragment(ArtifactRole.Payload, "catalogs/sample.json", projectionBytes));
        PackageDirectoryWriter.Write(package, document, plan, projections);

        var result = FactualPackageReader.Read(package);

        var projection = Assert.Single(result.Projections, fragment => fragment.CanonicalKey == "catalogs/sample.json");
        Assert.True(projectionBytes.AsSpan().SequenceEqual(projection.Payload.AsSpan()));
    }

    [Fact]
    public void Read_EmptySnapshot_NeverPublishesNotEvaluatedOrAZeroCoverageReadDefault()
    {
        using var output = TempOutputRoot.Create();
        var package = Path.Combine(output.DirectoryPath, "empty-package");
        var document = DomainMapper.ToWire(FactualSnapshot.Empty, Context);
        PackageDirectoryWriter.Write(package, document, LayoutPlanner.Plan(document, int.MaxValue));

        var result = FactualPackageReader.Read(package);

        Assert.NotEqual("not_evaluated", result.Certification.Status);
        Assert.True(result.Certification.Status is "passed" or "degraded" or "failed");
    }

    [Fact]
    public void Read_PackageMissingCoverageOrCertification_ThrowsRatherThanSynthesizingADefault()
    {
        using var output = TempOutputRoot.Create();
        var package = Path.Combine(output.DirectoryPath, "package-missing-coverage");
        var document = DomainMapper.ToWire(FactualSnapshot.Empty, Context);
        PackageDirectoryWriter.Write(package, document, LayoutPlanner.Plan(document, int.MaxValue));
        File.Delete(Path.Combine(package, "coverage.json"));
        var manifestPath = Path.Combine(package, "manifest.json");
        var manifest = CanonicalJson.Read<ManifestEnvelope>(File.ReadAllBytes(manifestPath));
        var withoutCoverage = manifest with
        {
            Artifacts = [.. manifest.Artifacts.Where(static entry => entry.Path != "coverage.json")],
        };
        File.WriteAllBytes(manifestPath, [.. CanonicalJson.Write(withoutCoverage)]);

        var exception = Assert.Throws<PublicationRejectedException>(() => FactualPackageReader.Read(package));
        Assert.Equal("missing-artifact", exception.Gate);
    }

    private static ImmutableArray<string> ContainsTargetIds(FactualSnapshot snapshot) =>
        [.. snapshot.ConfirmedRelations
            .Where(static relation => relation.Kind == RelationKind.Contains)
            .Select(static relation => relation.Target.Id.Value)];

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
