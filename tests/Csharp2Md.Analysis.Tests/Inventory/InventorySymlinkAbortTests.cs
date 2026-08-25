using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class InventorySymlinkAbortTests
{
    [Fact]
    [Trait("Requirement", "ROSE-02")]
    [Trait("Requirement", "ROSE-07")]
    [Trait("Requirement", "ROSE-63")]
    public async Task AnalyzeAsync_PlantedEscapeSymlink_UnpublishedNamingTheSymlinkAndKeepsPriorBytes()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-inv-symlink-abort-");
        try
        {
            var root = Path.Combine(tree.FullName, "root");
            var outside = Path.Combine(tree.FullName, "outside");
            var projectDir = Path.Combine(root, "App");
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(outside);
            File.WriteAllText(
                Path.Combine(projectDir, "App.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), "class Program;");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");
            var target = Path.Combine(outside, "secret.txt");
            File.WriteAllText(target, "classified");

            var store = new InMemoryTransactionalStore();
            var request = AnalysisRequest.Create([solutionPath]);
            var sessionKey = Path.GetFullPath(solutionPath);

            var first = await new AnalysisEngine(store).AnalyzeAsync(request, CancellationToken.None);
            var firstOutcome = Assert.Single(first.Solutions);
            Assert.Equal(PublicationStatus.Committed, firstOutcome.Status);
            Assert.True(store.TryGetPublication(sessionKey, out var prior));

            var symlink = Path.Combine(projectDir, "escape.link");
            File.CreateSymbolicLink(symlink, target);

            var second = await new AnalysisEngine(store).AnalyzeAsync(request, CancellationToken.None);

            var outcome = Assert.Single(second.Solutions);
            Assert.Equal(PublicationStatus.Unpublished, outcome.Status);
            Assert.False(outcome.StructuralCorruption);
            Assert.True(second.HasUnpublishedSolution);
            Assert.Contains(symlink, outcome.Detail, PathComparison());
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
                        .SequenceEqual(kept.ArtifactsInPublicationOrder[index].Payload.AsSpan()));
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
