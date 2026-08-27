using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class NoProjectorPublicationTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "RP-06")]
    public void FilesystemStore_WithoutProjector_PublishesNoProjectionArtifact()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var onDisk = FilesystemTestPaths.SnapshotFiles(child);
        AssertNoProjectionArtifact(onDisk.Keys);

        var manifest = CanonicalJson.Read<ManifestEnvelope>(onDisk["manifest.json"]);
        AssertNoProjectionArtifact(manifest.Artifacts.Select(static entry => entry.Path));
    }

    [Fact]
    [Trait("Requirement", "RP-06")]
    public void InMemoryStore_WithoutProjector_PublishesNoProjectionArtifact()
    {
        var session = new InMemoryTransactionalStore().Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        var artifacts = session.Commit().ArtifactsInPublicationOrder;

        AssertNoProjectionArtifact(artifacts.Select(static fragment => fragment.CanonicalKey));
        var manifest = CanonicalJson.Read<ManifestEnvelope>(artifacts[^1].Payload.AsSpan());
        AssertNoProjectionArtifact(manifest.Artifacts.Select(static entry => entry.Path));
    }

    private static void AssertNoProjectionArtifact(IEnumerable<string> keys)
    {
        Assert.All(keys, static key =>
        {
            Assert.False(key.StartsWith("projections/", StringComparison.Ordinal));
            Assert.False(key.StartsWith("catalogs/", StringComparison.Ordinal));
            Assert.False(key.StartsWith("postings/", StringComparison.Ordinal));
            Assert.False(key.StartsWith("source/", StringComparison.Ordinal));
            Assert.False(key.StartsWith("pages/", StringComparison.Ordinal));
            Assert.False(string.Equals(key, "retrieval.md", StringComparison.Ordinal));
            Assert.False(string.Equals(key, "AGENTS.md", StringComparison.Ordinal));
        });
    }
}
