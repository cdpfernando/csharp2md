using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

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

        sessionA.Stage(SolutionSnapshot());
        sessionB.Stage(CandidateSnapshot());

        var publicationA = sessionA.Commit();
        var publicationB = sessionB.Commit();

        Assert.Equal("solution-a", publicationA.SolutionKey);
        Assert.Equal("solution-b", publicationB.SolutionKey);
        Assert.Contains(publicationA.ArtifactsInPublicationOrder, fragment => fragment.CanonicalKey == "facts/structural.json");
        Assert.DoesNotContain(publicationA.ArtifactsInPublicationOrder, fragment => fragment.CanonicalKey == "relations/candidates.json");
        Assert.Contains(publicationB.ArtifactsInPublicationOrder, fragment => fragment.CanonicalKey == "relations/candidates.json");
        Assert.DoesNotContain(publicationB.ArtifactsInPublicationOrder, fragment => fragment.CanonicalKey == "facts/structural.json");
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

        var scanned = new[]
            {
                Path.Combine(storageRoot, "Wire"),
                Path.Combine(storageRoot, "Mapping"),
                Path.Combine(storageRoot, "Validation"),
            }
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Append(Path.Combine(storageRoot, "InMemoryTransactionalStore.cs"))
            .Where(path => File.Exists(path) && !IsGeneratedOutput(path));

        var offending = scanned
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
    [Trait("Requirement", "STOR-16")]
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

        Assert.Equal(ArtifactRole.Manifest, first.ArtifactsInPublicationOrder[^1].Role);
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
        second.Stage(CandidateSnapshot());
        var replacement = second.Commit();

        Assert.True(store.TryGetPublication("solution-a", out var stored));
        Assert.Equal(replacement, stored);
        Assert.Equal("solution-a", stored.SolutionKey);
        Assert.Contains(stored.ArtifactsInPublicationOrder, fragment => fragment.CanonicalKey == "relations/candidates.json");
        Assert.Equal(ArtifactRole.Manifest, stored.ArtifactsInPublicationOrder[^1].Role);
    }

    [Fact]
    [Trait("Requirement", "STOR-06")]
    [Trait("Requirement", "STOR-16")]
    [Trait("Requirement", "STOR-50")]
    public void Commit_EmptySnapshot_OmitsFamilyShardsIncludesRegistryAndManifestLast()
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        session.Stage(FactualSnapshot.Empty);
        var artifacts = session.Commit().ArtifactsInPublicationOrder;

        Assert.NotEmpty(artifacts);
        Assert.Equal(ArtifactRole.Manifest, artifacts[^1].Role);
        Assert.Equal("manifest.json", artifacts[^1].CanonicalKey);
        Assert.All(artifacts[..^1], fragment => Assert.Equal(ArtifactRole.Payload, fragment.Role));

        var payloadKeys = artifacts[..^1].Select(fragment => fragment.CanonicalKey).ToArray();
        Assert.Equal(payloadKeys.Order(StringComparer.Ordinal), payloadKeys);
        Assert.DoesNotContain(payloadKeys, key => key.StartsWith("facts/", StringComparison.Ordinal));
        Assert.DoesNotContain(payloadKeys, key => key.StartsWith("observations/", StringComparison.Ordinal));
        Assert.DoesNotContain(payloadKeys, key => key.StartsWith("relations/", StringComparison.Ordinal));

        var registry = Assert.Single(artifacts, fragment => fragment.CanonicalKey == PackagePublisher.RegistryKey);
        Assert.Equal(ArtifactRole.Payload, registry.Role);
        var expectedRegistry = DomainMapper.ToWire(FactualSnapshot.Empty, new ManifestContext("solution-a", "solution-a"))
            .TaxonomyRegistryCopy;
        Assert.True(registry.Payload.AsSpan().SequenceEqual(expectedRegistry.AsSpan()));

        var manifest = CanonicalJson.Read<ManifestEnvelope>(artifacts[^1].Payload.AsSpan());
        Assert.Equal("solution-a", manifest.SolutionKey);
        Assert.Equal("solution-a", manifest.SolutionFileName);
        Assert.All(manifest.Artifacts, entry => Assert.Equal(0, entry.Count));
    }

    [Fact]
    [Trait("Requirement", "STOR-33")]
    public void Commit_CandidateOnlySnapshot_SucceedsWithCandidatesShard()
    {
        var session = new InMemoryTransactionalStore().Open("solution-a");
        session.Stage(CandidateSnapshot());
        var publication = session.Commit();

        Assert.Equal("solution-a", publication.SolutionKey);
        Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role);
        Assert.Contains(
            publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey == "relations/candidates.json");
        Assert.DoesNotContain(
            publication.ArtifactsInPublicationOrder,
            fragment => fragment.CanonicalKey.StartsWith("facts/", StringComparison.Ordinal));

        var manifest = CanonicalJson.Read<ManifestEnvelope>(publication.ArtifactsInPublicationOrder[^1].Payload.AsSpan());
        var candidates = Assert.Single(manifest.Artifacts, entry => entry.CanonicalKey == "relations/candidates");
        Assert.Equal(1, candidates.Count);
    }

    [Fact]
    [Trait("Requirement", "STOR-24")]
    public void Commit_EmptySnapshot_CreatesNoFilesOrDirectories()
    {
        var probe = Path.Combine(Path.GetTempPath(), "csharp2md-stor24-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probe);
        try
        {
            Assert.Empty(Directory.GetFileSystemEntries(probe));

            var session = new InMemoryTransactionalStore().Open("solution-a");
            session.Stage(FactualSnapshot.Empty);
            session.Commit();

            Assert.Empty(Directory.GetFileSystemEntries(probe));
        }
        finally
        {
            Directory.Delete(probe, recursive: true);
        }
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

    private static FactualSnapshot SolutionSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));
        return new FactualSnapshot([solution], [], [], [], [], []);
    }

    private static FactualSnapshot CandidateSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var link = CandidateLink.Create(
            RelationKind.Contains,
            Solution.Create(solutionId).Reference,
            Project.Create(projectId).Reference,
            EvidenceChain.Create([
                new ObservationIdentity(
                    Solution.Create(solutionId).Reference,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    1)]));
        return new FactualSnapshot([], [], [], [link], [], []);
    }

    private static bool IsGeneratedOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }
}
