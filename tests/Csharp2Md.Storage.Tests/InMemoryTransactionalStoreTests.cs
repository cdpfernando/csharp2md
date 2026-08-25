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

    private static StagedFragment Fragment(ArtifactRole role, string canonicalKey, ImmutableArray<byte> payload) =>
        new(role, canonicalKey, payload);

    private static bool IsGeneratedOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }
}
