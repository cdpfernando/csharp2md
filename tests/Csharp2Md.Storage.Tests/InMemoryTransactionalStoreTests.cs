using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Storage.Tests;

public sealed class InMemoryTransactionalStoreTests
{
    [Fact]
    [Trait("Requirement", "ENG-21")]
    public void Open_IsolatesFragmentListsBySolutionKey()
    {
        var store = new InMemoryTransactionalStore();
        var sessionA = store.Open("solution-a");
        var sessionB = store.Open("solution-b");

        sessionA.Stage(Fragment(ArtifactRole.Payload, "a-only", [1]));
        sessionB.Stage(Fragment(ArtifactRole.Payload, "b-only", [2]));

        var publicationA = sessionA.Commit();
        var publicationB = sessionB.Commit();

        Assert.Equal("solution-a", publicationA.SolutionKey);
        Assert.Equal("solution-b", publicationB.SolutionKey);
        Assert.Equal(["a-only"], publicationA.ArtifactsInPublicationOrder.Select(fragment => fragment.CanonicalKey));
        Assert.Equal(["b-only"], publicationB.ArtifactsInPublicationOrder.Select(fragment => fragment.CanonicalKey));
        Assert.DoesNotContain(publicationA.ArtifactsInPublicationOrder, fragment => fragment.CanonicalKey == "b-only");
        Assert.DoesNotContain(publicationB.ArtifactsInPublicationOrder, fragment => fragment.CanonicalKey == "a-only");
    }

    [Fact]
    [Trait("Requirement", "ENG-21")]
    public void Open_AfterAbort_StartsANewSessionEmpty()
    {
        var store = new InMemoryTransactionalStore();
        var first = store.Open("solution-a");
        first.Stage(Fragment(ArtifactRole.Payload, "aborted", [9]));
        first.Abort();

        var second = store.Open("solution-a");
        second.Stage(Fragment(ArtifactRole.Payload, "kept", [4]));
        var publication = second.Commit();

        Assert.DoesNotContain(
            publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey == "aborted");
        var kept = Assert.Single(publication.ArtifactsInPublicationOrder);
        Assert.Equal("kept", kept.CanonicalKey);
        Assert.True(kept.Payload.AsSpan().SequenceEqual((ReadOnlySpan<byte>)[4]));
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
    public void Commit_PublishesPayloadsBeforeManifestEvenWhenManifestWasStagedFirst()
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        session.Stage(Fragment(ArtifactRole.Manifest, "manifest", [0x4D]));
        session.Stage(Fragment(ArtifactRole.Payload, "zeta", [0x5A]));
        session.Stage(Fragment(ArtifactRole.Payload, "alpha", [0x41]));

        var artifacts = session.Commit().ArtifactsInPublicationOrder;

        Assert.Equal(3, artifacts.Length);
        AssertFragment(artifacts[0], ArtifactRole.Payload, "alpha", [0x41]);
        AssertFragment(artifacts[1], ArtifactRole.Payload, "zeta", [0x5A]);
        AssertFragment(artifacts[2], ArtifactRole.Manifest, "manifest", [0x4D]);
    }

    [Fact]
    [Trait("Requirement", "ENG-27")]
    public void Commit_TwoStagingOrders_YieldByteIdenticalPublicationOrder()
    {
        var first = CommitInOrder(
            Fragment(ArtifactRole.Payload, "b", [2]),
            Fragment(ArtifactRole.Manifest, "m-z", [9]),
            Fragment(ArtifactRole.Payload, "a", [1]),
            Fragment(ArtifactRole.Manifest, "m-a", [8]));
        var second = CommitInOrder(
            Fragment(ArtifactRole.Manifest, "m-a", [8]),
            Fragment(ArtifactRole.Payload, "a", [1]),
            Fragment(ArtifactRole.Manifest, "m-z", [9]),
            Fragment(ArtifactRole.Payload, "b", [2]));

        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].Role, second[i].Role);
            Assert.Equal(first[i].CanonicalKey, second[i].CanonicalKey);
            Assert.True(
                first[i].Payload.AsSpan().SequenceEqual(second[i].Payload.AsSpan()),
                $"Payload bytes at index {i} differ.");
        }

        AssertFragment(first[0], ArtifactRole.Payload, "a", [1]);
        AssertFragment(first[1], ArtifactRole.Payload, "b", [2]);
        AssertFragment(first[2], ArtifactRole.Manifest, "m-a", [8]);
        AssertFragment(first[3], ArtifactRole.Manifest, "m-z", [9]);
    }

    [Fact]
    [Trait("Requirement", "ENG-22")]
    public void Commit_SecondCallOnTheSameSession_IsRejected()
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        session.Stage(Fragment(ArtifactRole.Payload, "once", [1]));
        var first = session.Commit();

        Assert.Equal("once", Assert.Single(first.ArtifactsInPublicationOrder).CanonicalKey);
        Assert.Throws<InvalidOperationException>(session.Commit);
    }

    private static ImmutableArray<StagedFragment> CommitInOrder(params StagedFragment[] fragments)
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        foreach (var fragment in fragments)
        {
            session.Stage(fragment);
        }

        return session.Commit().ArtifactsInPublicationOrder;
    }

    private static void AssertFragment(
        StagedFragment fragment,
        ArtifactRole role,
        string canonicalKey,
        ReadOnlySpan<byte> payload)
    {
        Assert.Equal(role, fragment.Role);
        Assert.Equal(canonicalKey, fragment.CanonicalKey);
        Assert.True(
            fragment.Payload.AsSpan().SequenceEqual(payload),
            $"Payload bytes for '{canonicalKey}' differ.");
    }

    private static StagedFragment Fragment(ArtifactRole role, string canonicalKey, ImmutableArray<byte> payload) =>
        new(role, canonicalKey, payload);

    private static bool IsGeneratedOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }
}
