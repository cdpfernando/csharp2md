using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;

namespace Csharp2Md.Storage.Tests;

[Collection(FilesystemStoreCollection.Name)]
public sealed class PublicationPipelineTests
{
    private const string SolutionKey = @"C:\src\Acme.sln";

    [Fact]
    public void Publish_EmptySnapshot_BothStoresProduceTheSameFragmentKeySet()
    {
        AssertEqualKeySets(FactualSnapshot.Empty);
    }

    [Fact]
    public void Publish_StructuralSnapshot_BothStoresProduceTheSameFragmentKeySet()
    {
        AssertEqualKeySets(StructuralSnapshot());
    }

    [Fact]
    public void Publish_ObservationAndRelationSnapshot_BothStoresProduceTheSameFragmentKeySet()
    {
        AssertEqualKeySets(ObservationAndContainsSnapshot());
    }

    [Fact]
    public void Publish_FilledSnapshot_ManifestIsLastInBothStores()
    {
        var (memory, filesystem) = PublishBoth(StructuralSnapshot());

        Assert.Equal(PackagePublisher.ManifestKey, memory[^1].CanonicalKey);
        Assert.Equal(PackagePublisher.ManifestKey, filesystem[^1].CanonicalKey);
        Assert.Equal(ArtifactRole.Manifest, memory[^1].Role);
        Assert.Equal(ArtifactRole.Manifest, filesystem[^1].Role);
        Assert.Equal(
            memory.Select(fragment => fragment.CanonicalKey),
            filesystem.Select(fragment => fragment.CanonicalKey));
    }

    [Fact]
    public void BothStores_DelegateCommitSequenceToPublicationPipeline()
    {
        var storageRoot = Path.Combine(StorageTestPaths.RepoRoot, "src", "Csharp2Md.Storage");
        var pipeline = File.ReadAllText(Path.Combine(storageRoot, "Mapping", "PublicationPipeline.cs"));
        var filesystem = File.ReadAllText(Path.Combine(storageRoot, "FilesystemTransactionalStore.cs"));
        var memory = File.ReadAllText(Path.Combine(storageRoot, "InMemoryTransactionalStore.cs"));

        Assert.Contains("DomainMapper.ToWire", pipeline, StringComparison.Ordinal);
        Assert.Contains("PackageValidator.Validate", pipeline, StringComparison.Ordinal);
        Assert.Contains("PackagePublisher.ToPublicationOrder", pipeline, StringComparison.Ordinal);
        Assert.Contains("PublicationPipeline.Publish", filesystem, StringComparison.Ordinal);
        Assert.Contains("PublicationPipeline.Publish", memory, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainMapper.ToWire", filesystem, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainMapper.ToWire", memory, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageValidator.Validate", filesystem, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageValidator.Validate", memory, StringComparison.Ordinal);
        Assert.DoesNotContain("PackagePublisher.ToPublicationOrder", filesystem, StringComparison.Ordinal);
        Assert.DoesNotContain("PackagePublisher.ToPublicationOrder", memory, StringComparison.Ordinal);
    }

    private static void AssertEqualKeySets(FactualSnapshot snapshot)
    {
        var (memory, filesystem) = PublishBoth(snapshot);
        Assert.Equal(
            memory.Select(fragment => fragment.CanonicalKey).Order(StringComparer.Ordinal),
            filesystem.Select(fragment => fragment.CanonicalKey).Order(StringComparer.Ordinal));
    }

    private static (ImmutableArray<StagedFragment> Memory, ImmutableArray<StagedFragment> Filesystem) PublishBoth(
        FactualSnapshot snapshot)
    {
        var memorySession = new InMemoryTransactionalStore().Open(SolutionKey);
        memorySession.Stage(snapshot);
        var memory = memorySession.Commit().ArtifactsInPublicationOrder;

        using var output = TempOutputRoot.Create();
        var filesystemSession = new FilesystemTransactionalStore(output.DirectoryPath).Open(SolutionKey);
        filesystemSession.Stage(snapshot);
        var filesystem = filesystemSession.Commit().ArtifactsInPublicationOrder;
        return (memory, filesystem);
    }

    private static FactualSnapshot StructuralSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        return new FactualSnapshot(
            [Solution.Create(solutionId), Project.Create(projectId)],
            [],
            [],
            [],
            [],
            []);
    }

    private static FactualSnapshot ObservationAndContainsSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var owner = Solution.Create(solutionId).Reference;
        var observation = Observation.Create(
            owner,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
        var relation = ConfirmedRelation.Create(
            RelationKind.Contains,
            owner,
            Project.Create(projectId).Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([observation.Identity]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
        return new FactualSnapshot(
            [Solution.Create(solutionId), Project.Create(projectId)],
            [observation],
            [relation],
            [],
            [],
            []);
    }
}
