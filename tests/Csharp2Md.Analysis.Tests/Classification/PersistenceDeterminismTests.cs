using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class PersistenceDeterminismTests
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    private static readonly string[] PersistencePayloadKeys =
    [
        "facts/persistence.json",
        "relations/confirmed/accesses-data.json",
        "relations/confirmed/operates-on.json",
        "relations/confirmed/maps-to.json",
        "relations/candidates.json",
        "relations/unresolved.json",
        "diagnostics.json",
    ];

    [Fact]
    [Trait("Requirement", "PK-45")]
    public async Task AnalyzeAsync_TwoFixtureClones_ProduceEqualPersistenceIdentitiesWithoutClonePaths()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-persistence-clone-");
        try
        {
            var cloneA = Path.Combine(tree.FullName, "clone-a");
            var cloneB = Path.Combine(tree.FullName, "clone-b");
            CopyClone(fixture, cloneA);
            CopyClone(fixture, cloneB);

            var rootA = Path.GetFullPath(cloneA);
            var rootB = Path.GetFullPath(cloneB);
            Assert.NotEqual(rootA, rootB);

            var publicationA = await PublishCloneAsync(cloneA);
            var publicationB = await PublishCloneAsync(cloneB);

            var identitiesA = PersistenceIdentities(publicationA);
            var identitiesB = PersistenceIdentities(publicationB);
            Assert.NotEmpty(identitiesA.Facts);
            Assert.NotEmpty(identitiesA.Relations);
            Assert.Equal(identitiesA.Facts, identitiesB.Facts);
            Assert.Equal(identitiesA.Relations, identitiesB.Relations);
            Assert.Equal(identitiesA.Candidates, identitiesB.Candidates);
            Assert.Equal(identitiesA.Unresolved, identitiesB.Unresolved);

            AssertNoAbsolutePath(PersistencePayloads(publicationA), rootA);
            AssertNoAbsolutePath(PersistencePayloads(publicationB), rootB);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "PK-46")]
    [Trait("Requirement", "PK-47")]
    public async Task AnalyzeAsync_SameSolutionTwice_ProducesByteIdenticalPersistencePayloadsWithoutAbsolutePaths()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var first = await PublishAsync(solutionPath);
        var second = await PublishAsync(solutionPath);

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);

        var firstPayloads = PersistenceFragments(first.Publication);
        var secondPayloads = PersistenceFragments(second.Publication);
        Assert.NotEmpty(firstPayloads);
        Assert.Equal(firstPayloads.Length, secondPayloads.Length);
        for (var index = 0; index < firstPayloads.Length; index++)
        {
            Assert.Equal(firstPayloads[index].CanonicalKey, secondPayloads[index].CanonicalKey);
            Assert.True(
                firstPayloads[index].Payload.AsSpan().SequenceEqual(secondPayloads[index].Payload.AsSpan()),
                $"Canonical payload bytes at '{firstPayloads[index].CanonicalKey}' differ across retries.");
        }

        AssertNoAbsolutePath(
            PersistencePayloads(first.Publication),
            Path.GetDirectoryName(Path.GetFullPath(solutionPath))!);
    }

    [Fact]
    [Trait("Requirement", "PK-45")]
    public async Task Execute_ReversedDocumentOrder_KeepsPersistenceIdentities()
    {
        var forward = await PersistenceSnapshotAsync(reverseDocuments: false);
        var reversed = await PersistenceSnapshotAsync(reverseDocuments: true);

        Assert.NotEmpty(forward.Facts);
        Assert.Equal(forward.Facts, reversed.Facts);
        Assert.Equal(forward.Relations, reversed.Relations);
        Assert.Equal(forward.Candidates, reversed.Candidates);
        Assert.Equal(forward.Unresolved, reversed.Unresolved);
    }

    private static async Task<PersistenceIdentitySet> PersistenceSnapshotAsync(bool reverseDocuments)
    {
        var context = new PipelineContext(new SwallowingSession(), AcmeOrdersSolutionPath());
        try
        {
            await new InventoryStage().ExecuteAsync(context, CancellationToken.None);
            Assert.True(
                context.CSharpDocuments.Length >= 2,
                $"Expected at least two documents to reverse, found {context.CSharpDocuments.Length}.");
            if (reverseDocuments)
            {
                var originalFirst = context.CSharpDocuments[0].RelativePath;
                context.CSharpDocuments = context.CSharpDocuments.Reverse().ToImmutableArray();
                Assert.NotEqual(originalFirst, context.CSharpDocuments[0].RelativePath);
            }

            await new SemanticAnalysisStage().ExecuteAsync(context, CancellationToken.None);
            await new ObservationExtractionStage().ExecuteAsync(context, CancellationToken.None);
            await new ClassificationAndPromotionStage([new PersistencePass()])
                .ExecuteAsync(context, CancellationToken.None);

            return SnapshotIdentities(context.Accumulator.ToSnapshot());
        }
        finally
        {
            context.BoundSolution?.Dispose();
        }
    }

    private static PersistenceIdentitySet SnapshotIdentities(FactualSnapshot snapshot)
    {
        var facts = Ordered(
            snapshot.Facts
                .Where(static fact => fact.Family is FactFamily.Persistence)
                .Select(static fact => fact.Reference.Id.Value));
        var relations = Ordered(
            snapshot.ConfirmedRelations
                .Where(static relation =>
                    relation.Kind is RelationKind.AccessesData or RelationKind.OperatesOn or RelationKind.MapsTo)
                .Select(static relation =>
                    string.Join('\u0000', relation.Kind, relation.Source.Id.Value, relation.Target.Id.Value)));
        var candidates = Ordered(
            snapshot.Candidates
                .Where(static link => link.Kind is RelationKind.MapsTo)
                .Select(static link =>
                    string.Join('\u0000', link.Kind, link.Source.Id.Value, link.ProposedTarget.Id.Value)));
        var unresolved = Ordered(
            snapshot.Unresolved.Select(static record =>
                string.Join('\u0000', record.Kind, record.Source.Id.Value, record.Cause)));
        return new PersistenceIdentitySet(facts, relations, candidates, unresolved);
    }

    private static PersistenceIdentitySet PersistenceIdentities(CommittedPublication publication)
    {
        var shard = ReadOptional<PersistenceFactsShard>(publication, "facts/persistence.json");
        var facts = Ordered(
            (shard?.DataStores.Select(dto => dto.Identity.Id) ?? [])
            .Concat(shard?.DataObjects.Select(dto => dto.Identity.Id) ?? [])
            .Concat(shard?.DataFields.Select(dto => dto.Identity.Id) ?? [])
            .Concat(shard?.DataOperations.Select(dto => dto.Identity.Id) ?? []));

        var relations = Ordered(
            PersistenceRelationKeys(publication, "relations/confirmed/accesses-data.json")
                .Concat(PersistenceRelationKeys(publication, "relations/confirmed/operates-on.json"))
                .Concat(PersistenceRelationKeys(publication, "relations/confirmed/maps-to.json")));

        var candidates = Ordered(
            ReadOptionalArray<CandidateLinkDto>(publication, "relations/candidates.json")
                .Where(static link => link.Kind == "maps-to")
                .Select(static link => string.Join('\u0000', link.Kind, link.Source.Id, link.ProposedTarget.Id)));

        var unresolved = Ordered(
            ReadOptionalArray<UnresolvedRecordDto>(publication, "relations/unresolved.json")
                .Where(static record =>
                    record.Kind is "accesses-data" or "operates-on" or "maps-to")
                .Select(static record => string.Join('\u0000', record.Kind, record.Source.Id, record.Cause)));

        return new PersistenceIdentitySet(facts, relations, candidates, unresolved);
    }

    private static IEnumerable<string> PersistenceRelationKeys(
        CommittedPublication publication,
        string canonicalKey) =>
        ReadOptionalArray<ConfirmedRelationDto>(publication, canonicalKey)
            .Select(static relation => string.Join('\u0000', relation.Kind, relation.Source.Id, relation.Target.Id));

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> PublishAsync(
        string solutionPath)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);
        var outcome = Assert.Single(result.Solutions);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static async Task<CommittedPublication> PublishCloneAsync(string cloneRoot)
    {
        var solutionPath = Path.Combine(cloneRoot, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected cloned solution at '{solutionPath}'.");
        var (outcome, publication) = await PublishAsync(solutionPath);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        return publication;
    }

    private static StagedFragment[] PersistenceFragments(CommittedPublication publication) =>
        [.. publication.ArtifactsInPublicationOrder
            .Where(fragment => PersistencePayloadKeys.Contains(fragment.CanonicalKey, StringComparer.Ordinal))];

    private static IReadOnlyList<(string Key, string Text)> PersistencePayloads(CommittedPublication publication) =>
        PersistenceFragments(publication)
            .Select(fragment => (fragment.CanonicalKey, Encoding.UTF8.GetString(fragment.Payload.ToArray())))
            .ToArray();

    private static void AssertNoAbsolutePath(
        IReadOnlyList<(string Key, string Text)> payloads,
        string absoluteRoot)
    {
        var full = Path.GetFullPath(absoluteRoot);
        var slash = full.Replace('\\', '/');
        var jsonEscaped = full.Replace("\\", "\\\\", StringComparison.Ordinal);
        Assert.NotEmpty(payloads);
        Assert.All(
            payloads,
            payload =>
            {
                Assert.DoesNotContain(full, payload.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(slash, payload.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(jsonEscaped, payload.Text, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static T? ReadOptional<T>(CommittedPublication publication, string canonicalKey)
        where T : class
    {
        var fragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == canonicalKey);
        return fragment is null ? null : CanonicalJson.Read<T>(fragment.Payload.AsSpan());
    }

    private static ImmutableArray<T> ReadOptionalArray<T>(CommittedPublication publication, string canonicalKey)
    {
        var fragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == canonicalKey);
        return fragment is null
            ? []
            : CanonicalJson.Read<ImmutableArray<T>>(fragment.Payload.AsSpan());
    }

    private static string[] Ordered(IEnumerable<string> values) =>
        values.OrderBy(static value => value, StringComparer.Ordinal).ToArray();

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static void CopyClone(string fixture, string cloneRoot)
    {
        Directory.CreateDirectory(cloneRoot);
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "Directory.Packages.props"),
            Path.Combine(cloneRoot, "Directory.Packages.props"));
        File.Copy(
            Path.Combine(AnalysisTestPaths.RepoRoot, "NuGet.Config"),
            Path.Combine(cloneRoot, "NuGet.Config"));
        File.WriteAllText(
            Path.Combine(cloneRoot, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """);

        var destination = Path.Combine(cloneRoot, "SyntheticSolution");
        foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(fixture, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(ExcludedSegments.Contains))
            {
                continue;
            }

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private sealed record PersistenceIdentitySet(
        string[] Facts,
        string[] Relations,
        string[] Candidates,
        string[] Unresolved);
}
