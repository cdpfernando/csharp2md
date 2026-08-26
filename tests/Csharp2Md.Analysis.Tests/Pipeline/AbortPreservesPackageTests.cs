using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Pipeline;

public sealed class AbortPreservesPackageTests
{
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    [Fact]
    [Trait("Requirement", "ROSE-63")]
    public async Task AnalyzeAsync_FixtureThenSymlinkEscape_KeepsPriorPublicationBytes()
    {
        var fixture = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(fixture), $"Expected fixture at '{fixture}'.");

        var tree = Directory.CreateTempSubdirectory("csharp2md-abort-preserves-");
        try
        {
            var clone = Path.Combine(tree.FullName, "clone");
            CopyClone(fixture, clone);
            var solutionPath = Path.Combine(clone, "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
            var sessionKey = Path.GetFullPath(solutionPath);
            var store = new InMemoryTransactionalStore();
            var request = AnalysisRequest.Create([solutionPath]);

            var first = await new AnalysisEngine(store).AnalyzeAsync(request, CancellationToken.None);
            var firstOutcome = Assert.Single(first.Solutions);
            Assert.Equal(PublicationStatus.Committed, firstOutcome.Status);
            Assert.True(store.TryGetPublication(sessionKey, out var prior));
            Assert.NotEmpty(prior.ArtifactsInPublicationOrder);

            var outside = Path.Combine(tree.FullName, "outside");
            Directory.CreateDirectory(outside);
            var target = Path.Combine(outside, "secret.txt");
            File.WriteAllText(target, "classified");
            var symlink = Path.Combine(clone, "SyntheticSolution", "Acme.Orders", "escape.link");
            File.CreateSymbolicLink(symlink, target);

            var second = await new AnalysisEngine(store).AnalyzeAsync(request, CancellationToken.None);
            var outcome = Assert.Single(second.Solutions);
            Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
            Assert.False(outcome.StructuralCorruption);
            Assert.False(string.IsNullOrWhiteSpace(outcome.Detail));
            Assert.Contains(symlink, outcome.Detail, PathComparison());
            Assert.True(second.HasUnpublishedSolution);

            Assert.True(store.TryGetPublication(sessionKey, out var kept));
            Assert.Equal(prior.SolutionKey, kept.SolutionKey);
            Assert.Equal(prior.ArtifactsInPublicationOrder.Length, kept.ArtifactsInPublicationOrder.Length);
            for (var index = 0; index < prior.ArtifactsInPublicationOrder.Length; index++)
            {
                Assert.Equal(prior.ArtifactsInPublicationOrder[index].Role, kept.ArtifactsInPublicationOrder[index].Role);
                Assert.Equal(
                    prior.ArtifactsInPublicationOrder[index].CanonicalKey,
                    kept.ArtifactsInPublicationOrder[index].CanonicalKey);
                Assert.True(
                    prior.ArtifactsInPublicationOrder[index].Payload.AsSpan()
                        .SequenceEqual(kept.ArtifactsInPublicationOrder[index].Payload.AsSpan()),
                    $"Payload bytes at '{prior.ArtifactsInPublicationOrder[index].CanonicalKey}' changed after abort.");
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

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
