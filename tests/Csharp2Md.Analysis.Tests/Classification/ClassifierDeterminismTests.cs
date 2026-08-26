using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ClassifierDeterminismTests
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    private static readonly string[] ClassifierPayloadKeys =
    [
        "facts/architecture.json",
        "facts/contract.json",
        "relations/confirmed/implements-operation.json",
        "relations/confirmed/uses-contract.json",
        "relations/candidates.json",
        "relations/unresolved.json",
        "diagnostics.json",
    ];

    [Fact]
    [Trait("Requirement", "EBC-32")]
    [Trait("Requirement", "EBC-40")]
    public async Task AnalyzeAsync_SameSolutionTwice_ProducesIdenticalClassifierIdentitiesAndCanonicalBytes()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var first = await PublishAsync(solutionPath);
        var second = await PublishAsync(solutionPath);

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);
        var firstIdentities = ClassifierIdentities(first.Publication);
        Assert.NotEmpty(firstIdentities.Facts);
        Assert.NotEmpty(firstIdentities.Relations);
        Assert.NotEmpty(firstIdentities.Candidates);
        AssertEqualIdentities(firstIdentities, ClassifierIdentities(second.Publication));

        var firstPayloads = CanonicalPayloads(first.Publication);
        var secondPayloads = CanonicalPayloads(second.Publication);
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
            ClassifierPayloads(first.Publication),
            Path.GetDirectoryName(Path.GetFullPath(solutionPath))!);
    }

    [Fact]
    [Trait("Requirement", "EBC-39")]
    [Trait("Requirement", "EBC-41")]
    public async Task AnalyzeAsync_TwoFixtureClones_ProduceEqualClassifierIdentitiesWithoutClonePaths()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-classifier-clone-");
        try
        {
            var cloneA = Path.Combine(tree.FullName, "clone-a");
            var cloneB = Path.Combine(tree.FullName, "clone-b");
            CopyClone(fixture, cloneA);
            CopyClone(fixture, cloneB);

            var rootA = Path.GetFullPath(cloneA);
            var rootB = Path.GetFullPath(cloneB);
            Assert.NotEqual(rootA, rootB);

            var (outcomeA, publicationA) = await AnalyzeCloneAsync(cloneA);
            var (outcomeB, publicationB) = await AnalyzeCloneAsync(cloneB);
            AssertClassifierCommitted(outcomeA, publicationA);
            AssertClassifierCommitted(outcomeB, publicationB);

            var identitiesA = ClassifierIdentities(publicationA);
            var identitiesB = ClassifierIdentities(publicationB);
            Assert.NotEmpty(identitiesA.Facts);
            Assert.NotEmpty(identitiesA.Relations);
            AssertEqualIdentities(identitiesA, identitiesB);

            AssertNoAbsolutePath(ClassifierPayloads(publicationA), rootA);
            AssertNoAbsolutePath(ClassifierPayloads(publicationB), rootB);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

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

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeCloneAsync(
        string cloneRoot)
    {
        var solutionPath = Path.Combine(cloneRoot, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected cloned solution at '{solutionPath}'.");
        return await PublishAsync(solutionPath);
    }

    private static void AssertClassifierCommitted(SolutionOutcome outcome, CommittedPublication publication)
    {
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(
            outcome.Stages[3].FactCount > 0,
            $"Classification fact count was {outcome.Stages[3].FactCount}. Detail={outcome.Detail}. Keys={string.Join(", ", publication.ArtifactsInPublicationOrder.Select(artifact => artifact.CanonicalKey))}.");
    }

    private static ClassifierIdentitySet ClassifierIdentities(CommittedPublication publication)
    {
        var architecture = ReadOptional<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var contracts = ReadOptional<ContractFactsShard>(publication, "facts/contract.json");
        var facts = Ordered(
            (architecture?.Components.Select(dto => dto.Identity.Id) ?? [])
            .Concat(architecture?.EntryPoints.Select(dto => dto.Identity.Id) ?? [])
            .Concat(architecture?.BoundaryOperations.Select(dto => dto.Identity.Id) ?? [])
            .Concat(architecture?.ExternalSystems.Select(dto => dto.Identity.Id) ?? [])
            .Concat(contracts?.Contracts.Select(dto => dto.Identity.Id) ?? [])
            .Concat(contracts?.ContractBindings.Select(dto => dto.Identity.Id) ?? [])
            .Concat(contracts?.ContractRevisions.Select(dto => dto.Identity.Id) ?? []));

        var relations = Ordered(
            ReadOptionalRelations(publication, "relations/confirmed/implements-operation.json")
                .Concat(ReadOptionalRelations(publication, "relations/confirmed/uses-contract.json"))
                .Select(RelationKey));

        var candidates = Ordered(
            ReadOptionalArray<CandidateLinkDto>(publication, "relations/candidates.json")
                .Select(link => string.Join('\u0000', link.Kind, link.Source.Id, link.ProposedTarget.Id)));

        var unresolved = Ordered(
            ReadOptionalArray<UnresolvedRecordDto>(publication, "relations/unresolved.json")
                .Select(record => string.Join('\u0000', record.Kind, record.Source.Id, record.Cause)));

        return new ClassifierIdentitySet(facts, relations, candidates, unresolved);
    }

    private static string RelationKey(ConfirmedRelationDto relation) =>
        string.Join('\u0000', relation.Kind, relation.Source.Id, relation.Target.Id);

    private static IReadOnlyList<(string Key, string Text)> ClassifierPayloads(CommittedPublication publication) =>
        publication.ArtifactsInPublicationOrder
            .Where(artifact => ClassifierPayloadKeys.Contains(artifact.CanonicalKey, StringComparer.Ordinal))
            .Select(artifact => (artifact.CanonicalKey, Encoding.UTF8.GetString(artifact.Payload.ToArray())))
            .ToArray();

    private static ImmutableArray<StagedFragment> CanonicalPayloads(CommittedPublication publication) =>
        [.. publication.ArtifactsInPublicationOrder
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .Where(fragment => fragment.CanonicalKey != "measurements.json")];

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

    private static IEnumerable<ConfirmedRelationDto> ReadOptionalRelations(
        CommittedPublication publication,
        string canonicalKey) =>
        ReadOptionalArray<ConfirmedRelationDto>(publication, canonicalKey);

    private static string[] Ordered(IEnumerable<string> values) =>
        values.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static void AssertEqualIdentities(ClassifierIdentitySet left, ClassifierIdentitySet right)
    {
        Assert.Equal(left.Facts, right.Facts);
        Assert.Equal(left.Relations, right.Relations);
        Assert.Equal(left.Candidates, right.Candidates);
        Assert.Equal(left.Unresolved, right.Unresolved);
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

    private sealed record ClassifierIdentitySet(
        string[] Facts,
        string[] Relations,
        string[] Candidates,
        string[] Unresolved);
}
