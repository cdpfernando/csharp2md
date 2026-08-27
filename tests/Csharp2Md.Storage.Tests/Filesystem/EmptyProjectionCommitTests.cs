using Csharp2Md.Analysis.Storage;
using Csharp2Md.Projection;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class EmptyProjectionCommitTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "RP-51")]
    public void Commit_EmptySnapshot_PublishesNoProjectionArtifact()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, new PackageProjector());
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var onDisk = FilesystemTestPaths.SnapshotFiles(child);
        AssertNoProjectionArtifact(onDisk.Keys);
        Assert.False(Directory.Exists(Path.Combine(child, "catalogs")));
        Assert.False(Directory.Exists(Path.Combine(child, "postings")));
        Assert.False(Directory.Exists(Path.Combine(child, "markdown")));
        Assert.False(Directory.Exists(Path.Combine(child, "source")));
        Assert.False(File.Exists(Path.Combine(child, "retrieval.md")));
        Assert.False(File.Exists(Path.Combine(child, "AGENTS.md")));

        var manifest = CanonicalJson.Read<ManifestEnvelope>(onDisk["manifest.json"]);
        AssertManifestListsOnDiskArtifacts(onDisk, manifest);
        AssertNoProjectionArtifact(manifest.Artifacts.Select(static entry => entry.Path));
    }

    [Fact]
    [Trait("Requirement", "RP-51")]
    public void Commit_EmptySnapshot_ManifestListsExactlyTheArtifactsOnDisk()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, new PackageProjector());
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        var publication = session.Commit();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var onDisk = FilesystemTestPaths.SnapshotFiles(child);
        Assert.Equal(
            new[]
            {
                "contracts/taxonomy-registry.json",
                "coverage.json",
                "diagnostics.json",
                "manifest.json",
                "measurements.json",
                "run-certification.json",
            },
            onDisk.Keys.Order(StringComparer.Ordinal));

        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            publication.ArtifactsInPublicationOrder
                .Single(static fragment => fragment.CanonicalKey == PackagePublisher.ManifestKey)
                .Payload
                .AsSpan());
        AssertManifestListsOnDiskArtifacts(onDisk, manifest);
        AssertNoProjectionArtifact(publication.ArtifactsInPublicationOrder.Select(static fragment => fragment.CanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "RP-51")]
    public void InMemoryCommit_EmptySnapshot_PublishesNoProjectionArtifact()
    {
        var session = new InMemoryTransactionalStore(new PackageProjector())
            .Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        var artifacts = session.Commit().ArtifactsInPublicationOrder;

        AssertNoProjectionArtifact(artifacts.Select(static fragment => fragment.CanonicalKey));
        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            artifacts.Single(static fragment => fragment.CanonicalKey == PackagePublisher.ManifestKey).Payload.AsSpan());
        Assert.Equal(
            artifacts[..^1].Select(static fragment => fragment.CanonicalKey).Order(StringComparer.Ordinal),
            manifest.Artifacts.Select(static entry => entry.Path).Order(StringComparer.Ordinal));
        AssertNoProjectionArtifact(manifest.Artifacts.Select(static entry => entry.Path));
    }

    private static void AssertManifestListsOnDiskArtifacts(
        IReadOnlyDictionary<string, byte[]> onDisk,
        ManifestEnvelope manifest)
    {
        Assert.True(onDisk.ContainsKey("manifest.json"));
        Assert.Equal(
            onDisk.Keys.Where(static key => key != "manifest.json").Order(StringComparer.Ordinal),
            manifest.Artifacts.Select(static entry => entry.Path).Order(StringComparer.Ordinal));
        Assert.All(manifest.Artifacts, entry => Assert.True(onDisk.ContainsKey(entry.Path), entry.Path));
    }

    private static void AssertNoProjectionArtifact(IEnumerable<string> keys)
    {
        Assert.All(keys, static key =>
        {
            Assert.False(key.StartsWith("catalogs/", StringComparison.Ordinal), key);
            Assert.False(key.StartsWith("postings/", StringComparison.Ordinal), key);
            Assert.False(key.StartsWith("markdown/", StringComparison.Ordinal), key);
            Assert.False(key.StartsWith("source/", StringComparison.Ordinal), key);
            Assert.False(key.StartsWith("pages/", StringComparison.Ordinal), key);
            Assert.False(string.Equals(key, "retrieval.md", StringComparison.Ordinal), key);
            Assert.False(string.Equals(key, "AGENTS.md", StringComparison.Ordinal), key);
        });
    }
}
