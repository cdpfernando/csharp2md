using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class TopologyDeterminismTests
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    private static readonly string[] TopologyPayloadKeys =
    [
        "facts/architecture.json",
        "facts/configuration.json",
        "relations/confirmed/belongs-to.json",
        "relations/confirmed/included-in.json",
        "relations/confirmed/configured-by.json",
        "relations/confirmed/targets.json",
        "relations/candidates.json",
        "relations/frontiers.json",
        "relations/unresolved.json",
        "diagnostics.json",
        "observations/configuration.json",
    ];

    [Fact]
    [Trait("Requirement", "CDC-49")]
    public async Task AnalyzeAsync_TwoFixtureClones_ProduceEqualTopologyIdentitiesAndByteIdenticalPayloads()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-topology-clone-");
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

            var identitiesA = TopologyIdentities(publicationA);
            var identitiesB = TopologyIdentities(publicationB);
            Assert.NotEmpty(identitiesA.Components);
            Assert.NotEmpty(identitiesA.DeploymentUnits);
            Assert.NotEmpty(identitiesA.Bindings);
            Assert.NotEmpty(identitiesA.Relations);
            AssertEqualIdentities(identitiesA, identitiesB);

            var payloadsA = TopologyFragments(publicationA);
            var payloadsB = TopologyFragments(publicationB);
            Assert.NotEmpty(payloadsA);
            Assert.Equal(payloadsA.Length, payloadsB.Length);
            for (var index = 0; index < payloadsA.Length; index++)
            {
                Assert.Equal(payloadsA[index].CanonicalKey, payloadsB[index].CanonicalKey);
                Assert.True(
                    payloadsA[index].Payload.AsSpan().SequenceEqual(payloadsB[index].Payload.AsSpan()),
                    $"Canonical payload bytes at '{payloadsA[index].CanonicalKey}' differ across clone paths.");
            }

            AssertNoAbsolutePath(TopologyPayloads(publicationA), rootA);
            AssertNoAbsolutePath(TopologyPayloads(publicationB), rootB);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-51")]
    public async Task AnalyzeAsync_SameSolutionTwice_ProducesIdenticalClassifierOutputAndCanonicalBytes()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var first = await PublishAsync(solutionPath);
        var second = await PublishAsync(solutionPath);

        Assert.Equal(PublicationStatus.Committed, first.Outcome.Status);
        Assert.Equal(PublicationStatus.Committed, second.Outcome.Status);
        AssertEqualIdentities(TopologyIdentities(first.Publication), TopologyIdentities(second.Publication));

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
    }

    [Fact]
    [Trait("Requirement", "CDC-49")]
    [Trait("Requirement", "CDC-51")]
    public async Task AnalyzeAsync_PublishedObservations_DoNotShareOwnerKindOrdinal()
    {
        var (_, publication) = await PublishAsync(AcmeOrdersSolutionPath());
        var configuration = CanonicalJson.Read<ImmutableArray<ObservationDto>>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "observations/configuration.json").Payload.AsSpan());
        Assert.NotEmpty(configuration);

        var keys = configuration
            .Select(observation =>
                $"{observation.Identity.Owner.Id}:{observation.Identity.Kind}:{observation.Identity.OccurrenceOrdinal}")
            .ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
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

    private static async Task<CommittedPublication> PublishCloneAsync(string cloneRoot)
    {
        var solutionPath = Path.Combine(cloneRoot, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected cloned solution at '{solutionPath}'.");
        var (outcome, publication) = await PublishAsync(solutionPath);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        return publication;
    }

    private static TopologyIdentitySet TopologyIdentities(CommittedPublication publication)
    {
        var architecture = ReadOptional<ArchitectureFactsShard>(publication, "facts/architecture.json");
        var configuration = ReadOptional<ConfigurationFactsShard>(publication, "facts/configuration.json");
        var components = Ordered(architecture?.Components.Select(dto => dto.Identity.Id) ?? []);
        var deploymentUnits = Ordered(architecture?.DeploymentUnits.Select(dto => dto.Identity.Id) ?? []);
        var bindings = Ordered(configuration?.ConfigurationBindings.Select(dto => dto.Identity.Id) ?? []);

        var relations = Ordered(
            publication.ArtifactsInPublicationOrder
                .Where(artifact => artifact.CanonicalKey.StartsWith("relations/confirmed/", StringComparison.Ordinal))
                .SelectMany(artifact => CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(artifact.Payload.AsSpan()))
                .Select(RelationKey));

        var candidates = Ordered(
            ReadOptionalArray<CandidateLinkDto>(publication, "relations/candidates.json")
                .Select(link => string.Join('\u0000', link.Kind, link.Source.Id, link.ProposedTarget.Id)));

        var frontiers = Ordered(
            ReadOptionalArray<OpenFrontierDto>(publication, "relations/frontiers.json")
                .Select(frontier =>
                    string.Join(
                        '\u0000',
                        frontier.Occurrence.Owner.Id,
                        frontier.Occurrence.Kind,
                        frontier.Occurrence.OccurrenceOrdinal.ToString(),
                        frontier.Cause)));

        var unresolved = Ordered(
            ReadOptionalArray<UnresolvedRecordDto>(publication, "relations/unresolved.json")
                .Select(record => string.Join('\u0000', record.Kind, record.Source.Id, record.Cause)));

        return new TopologyIdentitySet(
            components,
            deploymentUnits,
            bindings,
            relations,
            candidates,
            frontiers,
            unresolved);
    }

    private static string RelationKey(ConfirmedRelationDto relation) =>
        string.Join('\u0000', relation.Kind, relation.Source.Id, relation.Target.Id);

    private static StagedFragment[] TopologyFragments(CommittedPublication publication) =>
        [.. publication.ArtifactsInPublicationOrder
            .Where(fragment => TopologyPayloadKeys.Any(baseKey => ShardedFactsReader.IsFamilyMember(fragment.CanonicalKey, baseKey)))];

    private static IReadOnlyList<(string Key, string Text)> TopologyPayloads(CommittedPublication publication) =>
        TopologyFragments(publication)
            .Select(fragment => (fragment.CanonicalKey, Encoding.UTF8.GetString(fragment.Payload.ToArray())))
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

    private static void AssertEqualIdentities(TopologyIdentitySet left, TopologyIdentitySet right)
    {
        Assert.Equal(left.Components, right.Components);
        Assert.Equal(left.DeploymentUnits, right.DeploymentUnits);
        Assert.Equal(left.Bindings, right.Bindings);
        Assert.Equal(left.Relations, right.Relations);
        Assert.Equal(left.Candidates, right.Candidates);
        Assert.Equal(left.Frontiers, right.Frontiers);
        Assert.Equal(left.Unresolved, right.Unresolved);
    }

    private static T? ReadOptional<T>(CommittedPublication publication, string canonicalKey)
        where T : class
    {
        var hasAny = publication.ArtifactsInPublicationOrder
            .Any(artifact => ShardedFactsReader.IsFamilyMember(artifact.CanonicalKey, canonicalKey));
        return hasAny ? ShardedFactsReader.Read<T>(publication.ArtifactsInPublicationOrder, canonicalKey) : null;
    }

    private static ImmutableArray<T> ReadOptionalArray<T>(CommittedPublication publication, string canonicalKey)
    {
        var matches = publication.ArtifactsInPublicationOrder
            .Where(artifact => ShardedFactsReader.IsFamilyMember(artifact.CanonicalKey, canonicalKey))
            .OrderBy(static artifact => artifact.CanonicalKey, StringComparer.Ordinal);

        var records = ImmutableArray.CreateBuilder<T>();
        foreach (var artifact in matches)
        {
            records.AddRange(CanonicalJson.Read<ImmutableArray<T>>(artifact.Payload.AsSpan()));
        }

        return records.ToImmutable();
    }

    private static string[] Ordered(IEnumerable<string> values) =>
        values.OrderBy(value => value, StringComparer.Ordinal).ToArray();

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

    private sealed record TopologyIdentitySet(
        string[] Components,
        string[] DeploymentUnits,
        string[] Bindings,
        string[] Relations,
        string[] Candidates,
        string[] Frontiers,
        string[] Unresolved);
}
