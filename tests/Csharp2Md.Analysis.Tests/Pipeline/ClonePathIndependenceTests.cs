using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class ClonePathIndependenceTests
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    [Fact]
    [Trait("Requirement", "ROSE-52")]
    public async Task AnalyzeAsync_TwoFixtureClones_ProduceEqualStructuralAndObservationIdentities()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-clone-path-");
        try
        {
            var cloneA = Path.Combine(tree.FullName, "clone-a");
            var cloneB = Path.Combine(tree.FullName, "clone-b");
            CopyClone(fixture, cloneA);
            CopyClone(fixture, cloneB);

            var rootA = Path.GetFullPath(cloneA);
            var rootB = Path.GetFullPath(cloneB);
            Assert.NotEqual(rootA, rootB);

            var publicationA = await AnalyzeClone(cloneA);
            var publicationB = await AnalyzeClone(cloneB);

            var factIdsA = StructuralFactIds(publicationA);
            var factIdsB = StructuralFactIds(publicationB);
            var observationIdsA = ObservationIds(publicationA);
            var observationIdsB = ObservationIds(publicationB);

            Assert.NotEmpty(factIdsA);
            Assert.NotEmpty(observationIdsA);
            Assert.Equal(factIdsA, factIdsB);
            Assert.Equal(observationIdsA, observationIdsB);

            AssertNoClonePath(factIdsA.Concat(observationIdsA), rootA);
            AssertNoClonePath(factIdsB.Concat(observationIdsB), rootB);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static async Task<CommittedPublication> AnalyzeClone(string cloneRoot)
    {
        var solutionPath = Path.Combine(cloneRoot, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected cloned solution at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.Contains(
            Path.GetFullPath(cloneRoot),
            Path.GetFullPath(outcome.SolutionPath),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return publication;
    }

    private static string[] StructuralFactIds(CommittedPublication publication)
    {
        var structural = ShardedFactsReader.Read<StructuralFactsShard>(
            publication.ArtifactsInPublicationOrder, "facts/structural.json");
        return structural.Solutions.Select(solution => solution.Identity.Id)
            .Concat(structural.Projects.Select(project => project.Identity.Id))
            .Concat(structural.Documents.Select(document => document.Identity.Id))
            .Concat(structural.Symbols.Select(symbol => symbol.Identity.Id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ObservationIds(CommittedPublication publication)
    {
        return publication.ArtifactsInPublicationOrder
            .Where(artifact => artifact.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal))
            .SelectMany(artifact => CanonicalJson.Read<ImmutableArray<ObservationDto>>(artifact.Payload.AsSpan()))
            .Select(dto => Encoding.UTF8.GetString(CanonicalJson.Write(dto.Identity).AsSpan()))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertNoClonePath(IEnumerable<string> identities, string cloneRoot)
    {
        var full = Path.GetFullPath(cloneRoot);
        var slash = full.Replace('\\', '/');
        var jsonEscaped = full.Replace("\\", "\\\\", StringComparison.Ordinal);
        Assert.All(
            identities,
            identity =>
            {
                Assert.DoesNotContain(full, identity, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(slash, identity, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(jsonEscaped, identity, StringComparison.OrdinalIgnoreCase);
            });
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
}
