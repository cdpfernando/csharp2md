using System.Text.RegularExpressions;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class FilesystemEmptyCommitTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-14")]
    [Trait("Requirement", "STOR-19")]
    [Trait("Requirement", "STOR-22")]
    public void Commit_EmptySnapshot_WritesHashedChildUnderCreatedRoot()
    {
        using var output = TempOutputRoot.Uncreated();
        Assert.False(Directory.Exists(output.DirectoryPath));
        Assert.False(File.Exists(output.DirectoryPath));

        ITransactionalStore store = new FilesystemTransactionalStore(output.DirectoryPath);
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        var publication = session.Commit();

        Assert.True(Directory.Exists(output.DirectoryPath));
        Assert.Equal(SolutionKey, publication.SolutionKey);

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var childName = Path.GetFileName(child);
        Assert.True(Directory.Exists(child));
        Assert.NotEqual(Path.GetFullPath(output.DirectoryPath), Path.GetFullPath(child));
        Assert.Equal(Path.GetFullPath(output.DirectoryPath), Path.GetDirectoryName(Path.GetFullPath(child)));
        Assert.Equal(child, Assert.Single(Directory.GetFileSystemEntries(output.DirectoryPath)));

        Assert.Matches(new Regex("^s-[0-9a-f]{32}$", RegexOptions.CultureInvariant), childName);
        Assert.NotEqual(SolutionKey, childName);
        Assert.NotEqual("Acme Payments.sln", childName);
        Assert.False(Path.IsPathRooted(childName));
        Assert.DoesNotContain(Path.DirectorySeparatorChar, childName);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, childName);
        Assert.Equal("s-" + FilesystemTestPaths.SolutionHex(SolutionKey), childName);
    }

    [Fact]
    [Trait("Requirement", "MSC-02")]
    public void Open_StringPath_StillDerivesTheChildDirectoryFromTheAbsolutePath()
    {
        using var output = TempOutputRoot.Create();
        var path = Path.Combine(output.DirectoryPath, "Acme.Orders.slnx");
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var session = store.Open(path, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, path);
        Assert.True(Directory.Exists(child));
        Assert.Equal("s-" + FilesystemTestPaths.SolutionHex(path), Path.GetFileName(child));
    }

    [Fact]
    [Trait("Requirement", "STOR-05")]
    [Trait("Requirement", "STOR-06")]
    [Trait("Requirement", "STOR-16")]
    [Trait("Requirement", "STOR-50")]
    public void Commit_EmptySnapshot_WritesSchemaValidPackageWithRegistryAndManifestLast()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        var publication = session.Commit();

        var artifacts = publication.ArtifactsInPublicationOrder;
        Assert.NotEmpty(artifacts);
        Assert.Equal(ArtifactRole.Manifest, artifacts[^1].Role);
        Assert.Equal(PackagePublisher.ManifestKey, artifacts[^1].CanonicalKey);
        Assert.All(artifacts[..^1], fragment => Assert.Equal(ArtifactRole.Payload, fragment.Role));

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

        var committedRegistry = File.ReadAllBytes(
            Path.Combine(StorageTestPaths.RepoRoot, "contracts", "taxonomy-registry.json"));
        Assert.True(onDisk["contracts/taxonomy-registry.json"].AsSpan().SequenceEqual(committedRegistry));

        Assert.False(Directory.Exists(Path.Combine(child, "facts")));
        Assert.False(Directory.Exists(Path.Combine(child, "observations")));
        Assert.False(Directory.Exists(Path.Combine(child, "relations")));
        Assert.False(Directory.Exists(Path.Combine(child, "quarantine")));
        Assert.False(Directory.Exists(child + ".staging"));
        Assert.False(Directory.Exists(child + ".bak"));
        Assert.False(File.Exists(child + ".lock"));

        var manifest = CanonicalJson.Read<ManifestEnvelope>(onDisk["manifest.json"]);
        Assert.Equal(FilesystemTestPaths.SolutionHex(SolutionKey), manifest.SolutionKey);
        Assert.Equal("Acme Payments.sln", manifest.SolutionFileName);
        Assert.DoesNotContain(
            manifest.Artifacts,
            entry => entry.Count == 0
                && (entry.CanonicalKey.StartsWith("facts/", StringComparison.Ordinal)
                    || entry.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal)
                    || entry.CanonicalKey.StartsWith("relations/", StringComparison.Ordinal)));
    }
}
