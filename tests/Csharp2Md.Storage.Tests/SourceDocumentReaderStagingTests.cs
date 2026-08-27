using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Tests.Filesystem;

namespace Csharp2Md.Storage.Tests;

[Collection(FilesystemStoreCollection.Name)]
public sealed class SourceDocumentReaderStagingTests
{
    private const string SolutionKey = @"C:\src\Acme.sln";

    [Fact]
    [Trait("Requirement", "RP-57")]
    public void Commit_FilesystemCountingReader_RequestsEachDocumentAtMostOnce()
    {
        var first = DocumentId.Create("doc-a");
        var second = DocumentId.Create("doc-b");
        var unused = DocumentId.Create("doc-c");
        var reader = new CountingSourceDocumentReader(
            new Dictionary<DocumentId, ImmutableArray<byte>>
            {
                [first] = [1, 2, 3],
                [second] = [4, 5],
                [unused] = [9],
            });

        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var session = store.Open(SolutionKey, reader);
        StageDeferred(session, "source/a.bin", first, reader);
        StageDeferred(session, "source/b.bin", second, reader);
        session.Stage(FactualSnapshot.Empty);
        var publication = session.Commit();

        Assert.Equal(1, reader.RequestCount(first));
        Assert.Equal(1, reader.RequestCount(second));
        Assert.Equal(0, reader.RequestCount(unused));
        Assert.All(reader.RequestCounts.Values, count => Assert.True(count <= 1));

        var deferred = publication.ArtifactsInPublicationOrder.Where(fragment => fragment.IsDeferred).ToArray();
        Assert.Equal(2, deferred.Length);
        Assert.All(deferred, fragment => Assert.True(fragment.Payload.IsDefaultOrEmpty));
    }

    [Fact]
    [Trait("Requirement", "RP-57")]
    public void Commit_FilesystemDeferredFragment_WritesBytesAndPublicationKeepsEmptyPayload()
    {
        var document = DocumentId.Create("doc-a");
        ImmutableArray<byte> original = [10, 20, 30];
        var reader = new CountingSourceDocumentReader(
            new Dictionary<DocumentId, ImmutableArray<byte>> { [document] = original });

        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath);
        var session = store.Open(SolutionKey, reader);
        StageDeferred(session, "source/a.bin", document, reader);
        session.Stage(FactualSnapshot.Empty);
        var publication = session.Commit();

        var fragment = Assert.Single(publication.ArtifactsInPublicationOrder, item => item.CanonicalKey == "source/a.bin");
        Assert.True(fragment.IsDeferred);
        Assert.True(fragment.Payload.IsDefaultOrEmpty);

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var written = File.ReadAllBytes(Path.Combine(child, "source", "a.bin"));
        Assert.True(original.AsSpan().SequenceEqual(written));
        Assert.Equal(1, reader.RequestCount(document));
    }

    [Fact]
    [Trait("Requirement", "RP-57")]
    public void Commit_InMemoryCountingReader_RequestsEachDocumentAtMostOnce()
    {
        var first = DocumentId.Create("doc-a");
        var second = DocumentId.Create("doc-b");
        var reader = new CountingSourceDocumentReader(
            new Dictionary<DocumentId, ImmutableArray<byte>>
            {
                [first] = [1],
                [second] = [2],
            });

        var session = new InMemoryTransactionalStore().Open("solution-a", reader);
        StageDeferred(session, "source/a.bin", first, reader);
        StageDeferred(session, "source/b.bin", second, reader);
        session.Stage(FactualSnapshot.Empty);
        var publication = session.Commit();

        Assert.Equal(1, reader.RequestCount(first));
        Assert.Equal(1, reader.RequestCount(second));
        Assert.All(
            publication.ArtifactsInPublicationOrder.Where(fragment => fragment.IsDeferred),
            fragment => Assert.True(fragment.Payload.IsDefaultOrEmpty));
    }

    [Fact]
    [Trait("Requirement", "RP-05")]
    [Trait("Requirement", "RP-57")]
    public void Open_AcceptsReaderWithoutAddingAProjectorMemberOnTheStore()
    {
        var store = new InMemoryTransactionalStore();
        var session = store.Open("solution-a", EmptySourceDocumentReader.Instance);

        Assert.IsAssignableFrom<IStoreSession>(session);
        Assert.Null(typeof(ITransactionalStore).GetMethod("Project"));
        Assert.Null(typeof(IStoreSession).GetMethod("Project"));
    }

    private static void StageDeferred(
        IStoreSession session,
        string key,
        DocumentId document,
        CountingSourceDocumentReader reader)
    {
        var staging = Assert.IsAssignableFrom<IDeferredFragmentStaging>(session);
        staging.StageDeferred(StagedFragment.Deferred(
            ArtifactRole.Payload,
            key,
            () =>
            {
                Assert.True(reader.TryRead(document, out var bytes));
                return bytes;
            }));
    }

    private sealed class CountingSourceDocumentReader : ISourceDocumentReader
    {
        private readonly ImmutableDictionary<DocumentId, ImmutableArray<byte>> _bytes;
        private readonly Dictionary<DocumentId, int> _requests = [];

        public CountingSourceDocumentReader(IReadOnlyDictionary<DocumentId, ImmutableArray<byte>> bytes)
        {
            _bytes = bytes.ToImmutableDictionary();
            Documents = _bytes.Keys.OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray();
        }

        public ImmutableArray<DocumentId> Documents { get; }

        public IReadOnlyDictionary<DocumentId, int> RequestCounts => _requests;

        public int RequestCount(DocumentId document) => _requests.GetValueOrDefault(document);

        public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes)
        {
            _requests[document] = RequestCount(document) + 1;
            return _bytes.TryGetValue(document, out bytes);
        }
    }
}
