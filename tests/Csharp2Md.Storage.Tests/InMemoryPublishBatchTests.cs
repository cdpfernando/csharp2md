using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests;

[Collection(FilesystemStoreCollection.Name)]
public sealed class InMemoryPublishBatchTests
{
    private const string Orders = @"C:\src\Acme.Orders.slnx";

    [Fact]
    [Trait("Requirement", "MSC-01")]
    public void PublishBatch_SameBatchThroughBothStores_ProducesByteIdenticalManifestAndComposition()
    {
        using var output = TempOutputRoot.Create();
        var filesystem = new FilesystemTransactionalStore(output.DirectoryPath, composer: new CatalogComposer());
        var memory = new InMemoryTransactionalStore(composer: new CatalogComposer());
        var record = Commit(filesystem, memory, Orders);

        filesystem.PublishBatch([record]);
        memory.PublishBatch([record]);

        var published = memory.LastPublishedBatch;
        Assert.NotNull(published);
        var manifestOnDisk = File.ReadAllBytes(Path.Combine(output.DirectoryPath, "batch-manifest.json"));
        Assert.True(published.Manifest.AsSpan().SequenceEqual(manifestOnDisk));

        var fragment = Assert.Single(published.Composition);
        var compositionOnDisk = File.ReadAllBytes(Path.Combine(
            output.DirectoryPath,
            "composition",
            "components-and-deployment-units.json"));
        Assert.Equal("composition/components-and-deployment-units.json", fragment.CanonicalKey);
        Assert.True(fragment.Payload.AsSpan().SequenceEqual(compositionOnDisk));
    }

    [Fact]
    [Trait("Requirement", "MSC-01")]
    public void PublishBatch_ClearsTheAccumulator()
    {
        var store = new InMemoryTransactionalStore(composer: new CatalogComposer());
        var record = Commit(store, Orders);
        Assert.NotEmpty(store.AccumulatedContributions);

        store.PublishBatch([record]);

        Assert.Empty(store.AccumulatedContributions);
        Assert.NotNull(store.LastPublishedBatch);
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public void PublishBatch_ValidationFailure_SurfacesTheSameReasonCodeAsTheFilesystemStore()
    {
        using var output = TempOutputRoot.Create();
        var filesystem = new FilesystemTransactionalStore(output.DirectoryPath, composer: new InvalidComposer());
        var memory = new InMemoryTransactionalStore(composer: new InvalidComposer());
        var record = Commit(filesystem, memory, Orders);

        var filesystemException = Assert.Throws<PublicationRejectedException>(() => filesystem.PublishBatch([record]));
        var memoryException = Assert.Throws<PublicationRejectedException>(() => memory.PublishBatch([record]));

        Assert.Equal("batch-composition", filesystemException.Gate);
        Assert.Equal(filesystemException.Gate, memoryException.Gate);
        Assert.Equal(filesystemException.Detail, memoryException.Detail);
        Assert.Null(memory.LastPublishedBatch);
    }

    private static BatchSolutionRecord Commit(
        FilesystemTransactionalStore filesystem,
        InMemoryTransactionalStore memory,
        string solutionKey)
    {
        Commit(filesystem, solutionKey);
        return Commit(memory, solutionKey);
    }

    private static BatchSolutionRecord Commit(ITransactionalStore store, string solutionKey)
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
