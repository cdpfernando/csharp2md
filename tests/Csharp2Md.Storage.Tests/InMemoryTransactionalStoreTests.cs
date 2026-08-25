using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests;

public sealed class InMemoryTransactionalStoreTests
{
    [Fact]
    [Trait("Requirement", "ENG-21")]
    [Trait("Requirement", "STOR-15")]
    public void Open_IsolatesSessionsBySolutionKey()
    {
        var store = new InMemoryTransactionalStore();
        var sessionA = store.Open("solution-a");
        var sessionB = store.Open("solution-b");

        sessionA.Stage(FactualSnapshot.Empty);
        sessionB.Stage(FactualSnapshot.Empty);

        var publicationA = sessionA.Commit();
        var publicationB = sessionB.Commit();

        Assert.Equal("solution-a", publicationA.SolutionKey);
        Assert.Equal("solution-b", publicationB.SolutionKey);
        Assert.NotEqual(publicationA.SolutionKey, publicationB.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, publicationA.ArtifactsInPublicationOrder[^1].Role);
        Assert.Equal(ArtifactRole.Manifest, publicationB.ArtifactsInPublicationOrder[^1].Role);
        Assert.True(store.TryGetPublication("solution-a", out var storedA));
        Assert.True(store.TryGetPublication("solution-b", out var storedB));
        Assert.Equal(publicationA, storedA);
        Assert.Equal(publicationB, storedB);
        Assert.NotEqual(storedA.SolutionKey, storedB.SolutionKey);
    }

    [Fact]
    [Trait("Requirement", "ENG-21")]
    public void Open_AfterAbort_StartsANewSessionEmpty()
    {
        var store = new InMemoryTransactionalStore();
        var first = store.Open("solution-a");
        first.Stage(FactualSnapshot.Empty);
        first.Abort();
        Assert.False(store.TryGetPublication("solution-a", out _));

        var second = store.Open("solution-a");
        second.Stage(FactualSnapshot.Empty);
        var publication = second.Commit();

        Assert.True(store.TryGetPublication("solution-a", out var stored));
        Assert.Equal(publication, stored);
        Assert.Equal("solution-a", publication.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role);
    }

    [Fact]
    [Trait("Requirement", "ENG-21")]
    public void StorageProductionSources_DoNotUseSystemIo()
    {
        var storageRoot = Path.Combine(StorageTestPaths.RepoRoot, "src", "Csharp2Md.Storage");
        Assert.True(Directory.Exists(storageRoot), $"Storage project was not found at '{storageRoot}'.");

        var offending = Directory.EnumerateFiles(storageRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOutput(path))
            .Select(path => (path, text: File.ReadAllText(path)))
            .FirstOrDefault(file =>
                file.text.Contains("using System.IO", StringComparison.Ordinal)
                || file.text.Contains("System.IO.", StringComparison.Ordinal));

        Assert.True(
            string.IsNullOrEmpty(offending.path),
            $"Csharp2Md.Storage must not use System.IO, but '{offending.path}' does.");
    }

    [Fact]
    [Trait("Requirement", "ENG-23")]
    public void Commit_PublishesManifestLast()
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        session.Stage(FactualSnapshot.Empty);

        var artifacts = session.Commit().ArtifactsInPublicationOrder;

        Assert.NotEmpty(artifacts);
        Assert.Equal(ArtifactRole.Manifest, artifacts[^1].Role);
    }

    [Fact]
    [Trait("Requirement", "ENG-27")]
    public void Commit_TwoStagingOrders_YieldByteIdenticalPublicationOrder()
    {
        var first = CommitInOrder(FactualSnapshot.Empty, FactualSnapshot.Empty);
        var second = CommitInOrder(FactualSnapshot.Empty, FactualSnapshot.Empty);

        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].Role, second[i].Role);
            Assert.Equal(first[i].CanonicalKey, second[i].CanonicalKey);
            Assert.True(
                first[i].Payload.AsSpan().SequenceEqual(second[i].Payload.AsSpan()),
                $"Payload bytes at index {i} differ.");
        }

        Assert.Equal(ArtifactRole.Manifest, first[^1].Role);
        Assert.Equal(ArtifactRole.Manifest, second[^1].Role);
    }

    [Fact]
    [Trait("Requirement", "ENG-22")]
    public void Commit_SecondCallOnTheSameSession_IsRejected()
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        session.Stage(FactualSnapshot.Empty);
        var first = session.Commit();

        Assert.Equal(ArtifactRole.Manifest, Assert.Single(first.ArtifactsInPublicationOrder).Role);
        Assert.Throws<InvalidOperationException>(session.Commit);
    }

    [Fact]
    [Trait("Requirement", "ENG-25")]
    public void AbortThenCommitOfNewSession_DoesNotPublishTheAbortedSession()
    {
        var store = new InMemoryTransactionalStore();
        var first = store.Open("solution-a");
        first.Stage(FactualSnapshot.Empty);
        first.Abort();

        var second = store.Open("solution-a");
        second.Stage(FactualSnapshot.Empty);
        var publication = second.Commit();

        Assert.Equal("solution-a", publication.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role);
        Assert.True(store.TryGetPublication("solution-a", out var stored));
        Assert.Equal(publication, stored);
    }

    [Fact]
    [Trait("Requirement", "ENG-24")]
    public void CommitThenAbortSecondSession_LeavesPriorPublicationUnchanged()
    {
        var store = new InMemoryTransactionalStore();
        var first = store.Open("solution-a");
        first.Stage(FactualSnapshot.Empty);
        var prior = first.Commit();

        var second = store.Open("solution-a");
        second.Stage(FactualSnapshot.Empty);
        second.Abort();

        Assert.True(store.TryGetPublication("solution-a", out var stored));
        Assert.Equal(prior, stored);
        Assert.Equal(prior.SolutionKey, stored.SolutionKey);
        Assert.Equal(prior.ArtifactsInPublicationOrder.Length, stored.ArtifactsInPublicationOrder.Length);
        Assert.Equal(prior.ArtifactsInPublicationOrder[^1].Role, stored.ArtifactsInPublicationOrder[^1].Role);
        Assert.Equal(prior.ArtifactsInPublicationOrder[^1].CanonicalKey, stored.ArtifactsInPublicationOrder[^1].CanonicalKey);
        Assert.True(
            prior.ArtifactsInPublicationOrder[^1].Payload.AsSpan()
                .SequenceEqual(stored.ArtifactsInPublicationOrder[^1].Payload.AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "ENG-24")]
    public void FirstRunAbort_LeavesNoPublication()
    {
        var store = new InMemoryTransactionalStore();
        var session = store.Open("solution-a");
        session.Stage(FactualSnapshot.Empty);
        session.Abort();

        Assert.False(store.TryGetPublication("solution-a", out _));
    }

    [Fact]
    [Trait("Requirement", "ENG-24")]
    public void LaterSuccessfulCommit_ReplacesThePublicationForTheKey()
    {
        var store = new InMemoryTransactionalStore();
        var first = store.Open("solution-a");
        first.Stage(FactualSnapshot.Empty);
        first.Commit();

        var second = store.Open("solution-a");
        second.Stage(FactualSnapshot.Empty);
        var replacement = second.Commit();

        Assert.True(store.TryGetPublication("solution-a", out var stored));
        Assert.Equal(replacement, stored);
        Assert.Equal("solution-a", stored.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, stored.ArtifactsInPublicationOrder[^1].Role);
    }

    private static ImmutableArray<StagedFragment> CommitInOrder(params FactualSnapshot[] snapshots)
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        foreach (var snapshot in snapshots)
        {
            session.Stage(snapshot);
        }

        return session.Commit().ArtifactsInPublicationOrder;
    }

    private static bool IsGeneratedOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }
}
