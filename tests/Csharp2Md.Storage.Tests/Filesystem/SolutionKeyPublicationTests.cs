using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class SolutionKeyPublicationTests
{
    [Fact]
    [Trait("Requirement", "MSC-07")]
    public void Commit_SameSnapshotThroughBothStores_PublishesIdenticalSolutionKey()
    {
        var path = Path.Combine(Path.GetTempPath(), "clone-a", "Acme.Orders.slnx");
        var expected = SolutionCoordinate.For(path).Identity.Value;

        var memory = new InMemoryTransactionalStore();
        var memorySession = memory.Open(path, EmptySourceDocumentReader.Instance);
        memorySession.Stage(FactualSnapshot.Empty);
        var memoryPublication = memorySession.Commit();
        var memoryManifest = CanonicalJson.Read<ManifestEnvelope>(
            memoryPublication.ArtifactsInPublicationOrder[^1].Payload.AsSpan());

        using var output = TempOutputRoot.Create();
        var filesystem = new FilesystemTransactionalStore(output.DirectoryPath);
        var filesystemSession = filesystem.Open(path, EmptySourceDocumentReader.Instance);
        filesystemSession.Stage(FactualSnapshot.Empty);
        filesystemSession.Commit();
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, path);
        var filesystemManifest = CanonicalJson.Read<ManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(child, "manifest.json")));

        Assert.Equal(expected, memoryManifest.SolutionKey);
        Assert.Equal(expected, filesystemManifest.SolutionKey);
        Assert.Equal(memoryManifest.SolutionKey, filesystemManifest.SolutionKey);
        Assert.False(Path.IsPathRooted(memoryManifest.SolutionKey));
    }
}
