using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class FilesystemPublishBatchTests
{
    private const string Orders = @"C:\src\Acme.Orders.slnx";
    private const string Payments = @"C:\src\Acme.Payments.slnx";

    [Fact]
    [Trait("Requirement", "MSC-01")]
    public void PublishBatch_SuccessfulBatch_WritesManifestAndCompositionAtTheRoot()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new CatalogComposer());
        var record = Commit(store, Orders);

        store.PublishBatch([record]);

        var manifestPath = Path.Combine(output.DirectoryPath, "batch-manifest.json");
        Assert.True(File.Exists(manifestPath));
        var envelope = CanonicalJson.Read<BatchManifestEnvelope>(File.ReadAllBytes(manifestPath));
        Assert.Equal(record.Identity.Value, Assert.Single(envelope.Solutions).Identity);
        Assert.Equal(
            "composition/components-and-deployment-units.json",
            Assert.Single(envelope.Artifacts).CanonicalKey);
        Assert.True(File.Exists(Path.Combine(
            output.DirectoryPath,
            "composition",
            "components-and-deployment-units.json")));
    }

    [Fact]
    [Trait("Requirement", "MSC-14")]
    public void PublishBatch_EarlierManifest_IsReplacedNotMerged()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var record = Commit(store, Orders);
        File.WriteAllText(
            Path.Combine(output.DirectoryPath, "batch-manifest.json"),
            """{"schema_version":1,"complete":false,"stale":true,"solutions":[]}""");

        store.PublishBatch([record]);

        var json = File.ReadAllText(Path.Combine(output.DirectoryPath, "batch-manifest.json"));
        Assert.DoesNotContain("stale", json, StringComparison.Ordinal);
        var envelope = CanonicalJson.Read<BatchManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(output.DirectoryPath, "batch-manifest.json")));
        Assert.True(envelope.Complete);
        Assert.Equal(record.Identity.Value, Assert.Single(envelope.Solutions).Identity);
    }

    [Fact]
    [Trait("Requirement", "MSC-13")]
    public void PublishBatch_UnmappedPackageDirectory_IsLeftByteIdenticalAndUnreferenced()
    {
        using var output = TempOutputRoot.Create();
        var stale = Path.Combine(output.DirectoryPath, "s-" + new string('f', 32));
        Directory.CreateDirectory(stale);
        var keep = Path.Combine(stale, "keep-me.txt");
        File.WriteAllText(keep, "operator-owned");
        var prior = FilesystemTestPaths.SnapshotFiles(stale);

        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var record = Commit(store, Orders);
        store.PublishBatch([record]);

        var after = FilesystemTestPaths.SnapshotFiles(stale);
        AssertEqualSnapshots(prior, after);
        var envelope = CanonicalJson.Read<BatchManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(output.DirectoryPath, "batch-manifest.json")));
        Assert.DoesNotContain(
            envelope.Solutions,
            entry => string.Equals(entry.PackageDirectory, Path.GetFileName(stale), StringComparison.Ordinal));
        Assert.Equal(record.Identity.Value, Assert.Single(envelope.Solutions).Identity);
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public void PublishBatch_ValidationFailure_LeavesPackagesByteIdenticalAndWritesNoManifest()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new InvalidComposer());
        var record = Commit(store, Orders);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, Orders);
        var prior = FilesystemTestPaths.SnapshotFiles(child);

        var exception = Assert.Throws<PublicationRejectedException>(() => store.PublishBatch([record]));

        Assert.Equal("batch-composition", exception.Gate);
        AssertEqualSnapshots(prior, FilesystemTestPaths.SnapshotFiles(child));
        Assert.False(File.Exists(Path.Combine(output.DirectoryPath, "batch-manifest.json")));
        Assert.False(Directory.Exists(Path.Combine(output.DirectoryPath, "composition")));
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public void PublishBatch_IoFailure_LeavesPackagesByteIdenticalAndWritesNoManifest()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new CatalogComposer());
        var record = Commit(store, Orders);
        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, Orders);
        var prior = FilesystemTestPaths.SnapshotFiles(child);
        File.WriteAllText(Path.Combine(output.DirectoryPath, "composition.staging"), "blocked");

        var exception = Assert.Throws<PublicationRejectedException>(() => store.PublishBatch([record]));

        Assert.Equal("io", exception.Gate);
        AssertEqualSnapshots(prior, FilesystemTestPaths.SnapshotFiles(child));
        Assert.False(File.Exists(Path.Combine(output.DirectoryPath, "batch-manifest.json")));
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public void PublishBatch_HeldRootLock_RaisesLockAndWritesNothing()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var record = Commit(store, Orders);
        var lockPath = output.DirectoryPath + ".lock";
        try
        {
            using var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            var exception = Assert.Throws<PublicationRejectedException>(() => store.PublishBatch([record]));

            Assert.Equal("lock", exception.Gate);
            Assert.Contains(output.DirectoryPath, exception.Detail, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(output.DirectoryPath, "batch-manifest.json")));
        }
        finally
        {
            TryDelete(lockPath);
        }
    }

    [Fact]
    [Trait("Requirement", "MSC-40")]
    public void PublishBatch_MissingOutputRoot_CreatesItAndPublishes()
    {
        using var output = TempOutputRoot.Uncreated();
        Assert.False(Directory.Exists(output.DirectoryPath));
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var coordinate = SolutionCoordinate.For(Orders);
        var record = new BatchSolutionRecord(
            coordinate.Identity,
            coordinate.SolutionFileName,
            PublicationStatus.Committed,
            null);

        store.PublishBatch([record]);

        Assert.True(Directory.Exists(output.DirectoryPath));
        Assert.True(File.Exists(Path.Combine(output.DirectoryPath, "batch-manifest.json")));
    }

    [Fact]
    [Trait("Requirement", "MSC-39")]
    public void PublishBatch_ContributionPresentButPackageDirectoryMissing_PublishesTheSolutionAsUnpublished()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new CatalogComposer());
        var missing = Commit(store, Orders);
        var sibling = Commit(store, Payments);
        var siblingDir = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, Payments);
        var priorSibling = FilesystemTestPaths.SnapshotFiles(siblingDir);
        var missingDir = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, Orders);
        Directory.Delete(missingDir, recursive: true);
        Assert.False(Directory.Exists(missingDir));

        store.PublishBatch([missing, sibling]);

        var envelope = CanonicalJson.Read<BatchManifestEnvelope>(
            File.ReadAllBytes(Path.Combine(output.DirectoryPath, "batch-manifest.json")));
        var unpublished = Assert.Single(
            envelope.Solutions,
            entry => string.Equals(entry.Identity, missing.Identity.Value, StringComparison.Ordinal));
        Assert.Equal("unpublished", unpublished.Status);
        Assert.False(envelope.Complete);
        Assert.Equal("solution-unpublished", envelope.IncompleteScopeReason);
        Assert.Null(unpublished.FailingStage);
        var committed = Assert.Single(
            envelope.Solutions,
            entry => string.Equals(entry.Identity, sibling.Identity.Value, StringComparison.Ordinal));
        Assert.Equal("committed", committed.Status);
        AssertEqualSnapshots(priorSibling, FilesystemTestPaths.SnapshotFiles(siblingDir));
        var composition = File.ReadAllText(Path.Combine(
            output.DirectoryPath,
            "composition",
            "components-and-deployment-units.json"));
        Assert.DoesNotContain(missing.Identity.Value, composition, StringComparison.Ordinal);
        Assert.Contains(sibling.Identity.Value, composition, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-01")]
    public void PublishBatch_ClearsTheAccumulator()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, composer: new CatalogComposer());
        var record = Commit(store, Orders);
        Assert.NotEmpty(store.AccumulatedContributions);

        store.PublishBatch([record]);

        Assert.Empty(store.AccumulatedContributions);
    }

    private static BatchSolutionRecord Commit(FilesystemTransactionalStore store, string solutionKey)
    {
        var session = store.Open(solutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();
        var coordinate = SolutionCoordinate.For(solutionKey);
        return new BatchSolutionRecord(
            coordinate.Identity,
            coordinate.SolutionFileName,
            PublicationStatus.Committed,
            null);
    }

    private static void AssertEqualSnapshots(
        IReadOnlyDictionary<string, byte[]> left,
        IReadOnlyDictionary<string, byte[]> right)
    {
        Assert.Equal(left.Keys.Order(StringComparer.Ordinal), right.Keys.Order(StringComparer.Ordinal));
        foreach (var key in left.Keys)
        {
            Assert.True(left[key].AsSpan().SequenceEqual(right[key]), $"Bytes at '{key}' changed.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class CatalogComposer : IBatchComposer
    {
        public SolutionContribution Contribute(
            PublishedPackageView view,
            SolutionCoordinate coordinate,
            string packageDirectory) =>
            new(
                coordinate.Identity.Value,
                coordinate.SolutionFileName,
                packageDirectory,
                [],
                [],
                [new ContributedNamedIdentity("fact-1", "Component", "api", "facts/architecture.json", 0)],
                [],
                []);

        public ImmutableArray<StagedFragment> Compose(BatchView batch)
        {
            var contribution = Assert.Single(batch.Contributions);
            var json = $$"""
                [{
                  "canonical_name": "api",
                  "entries": [{
                    "fact_id": "fact-1",
                    "solution_identity": "{{contribution.SolutionIdentity}}",
                    "artifact_key": "facts/architecture.json",
                    "ordinal": 0
                  }]
                }]
                """;
            return
            [
                new StagedFragment(
                    ArtifactRole.Payload,
                    "composition/components-and-deployment-units.json",
                    Encoding.UTF8.GetBytes(json).ToImmutableArray()),
            ];
        }
    }

    private sealed class InvalidComposer : IBatchComposer
    {
        public SolutionContribution Contribute(
            PublishedPackageView view,
            SolutionCoordinate coordinate,
            string packageDirectory) =>
            new(coordinate.Identity.Value, coordinate.SolutionFileName, packageDirectory, [], [], [], [], []);

        public ImmutableArray<StagedFragment> Compose(BatchView batch)
        {
            const string json = """[{"source_solution_identity":"missing","target_solution_identity":"also-missing"}]""";
            return
            [
                new StagedFragment(
                    ArtifactRole.Payload,
                    "composition/cross-solution-relations.json",
                    Encoding.UTF8.GetBytes(json).ToImmutableArray()),
            ];
        }
    }
}
