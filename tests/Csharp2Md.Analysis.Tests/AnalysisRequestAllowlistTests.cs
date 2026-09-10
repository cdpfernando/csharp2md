using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests;

/// <summary>
/// GCPC-030: the caller-supplied document allowlist admits the listed documents without admitting
/// any other excluded class, and an entry naming a path outside the authorized root is rejected.
/// The solution file lives at the root and the project in a subdirectory, so the authorized root is
/// the tree root and document relative paths are prefixed with the project directory name.
/// </summary>
public sealed class AnalysisRequestAllowlistTests
{
    [Fact]
    [Trait("Requirement", "GCPC-030")]
    public async Task AnalyzeAsync_AllowlistedTypeScriptFile_IsInventoriedWhileSiblingJavaScriptStaysExcluded()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-allowlist-");
        try
        {
            var solutionPath = WriteSolution(tree.FullName, out var projectDir);
            File.WriteAllText(Path.Combine(projectDir, "app.ts"), "export const x = 1;");
            File.WriteAllText(Path.Combine(projectDir, "app.js"), "export const x = 1;");

            var request = AnalysisRequest.Create([solutionPath], ["App/app.ts"]);
            var documentPaths = await AnalyzeStructuralDocumentsAsync(solutionPath, request);

            Assert.Contains("App/app.ts", documentPaths);
            Assert.DoesNotContain("App/app.js", documentPaths);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-030")]
    public void Create_AllowlistEntryOutsideAuthorizedRoot_IsRejected()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-allowlist-escape-");
        try
        {
            var solutionPath = WriteSolution(tree.FullName, out _);
            var outsideDirectory = Directory.CreateTempSubdirectory("csharp2md-allowlist-outside-");
            try
            {
                File.WriteAllText(Path.Combine(outsideDirectory.FullName, "secret.txt"), "outside");
                var escapingEntry = Path.GetRelativePath(tree.FullName, Path.Combine(outsideDirectory.FullName, "secret.txt"))
                    .Replace('\\', '/');

                var exception = Assert.Throws<ArgumentException>(
                    () => AnalysisRequest.Create([solutionPath], [escapingEntry]));

                Assert.Contains(escapingEntry, exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                outsideDirectory.Delete(recursive: true);
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-030")]
    public async Task AnalyzeAsync_EmptyAllowlist_ChangesNothing()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-allowlist-empty-");
        try
        {
            var solutionPath = WriteSolution(tree.FullName, out var projectDir);
            File.WriteAllText(Path.Combine(projectDir, "app.ts"), "export const x = 1;");

            var withDefault = await AnalyzeStructuralDocumentsAsync(solutionPath, AnalysisRequest.Create([solutionPath]));
            var withEmptyAllowlist = await AnalyzeStructuralDocumentsAsync(
                solutionPath,
                AnalysisRequest.Create([solutionPath], []));

            Assert.Equal(withDefault, withEmptyAllowlist);
            Assert.DoesNotContain("App/app.ts", withDefault);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    /// <summary>Writes App.slnx at <paramref name="rootDir"/> and the project under "App/" beneath it.</summary>
    private static string WriteSolution(string rootDir, out string projectDir)
    {
        projectDir = Path.Combine(rootDir, "App");
        Directory.CreateDirectory(projectDir);
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
        var solutionPath = Path.Combine(rootDir, "App.slnx");
        File.WriteAllText(solutionPath, """<Solution><Project Path="App/App.csproj" /></Solution>""");
        return solutionPath;
    }

    private static async Task<HashSet<string>> AnalyzeStructuralDocumentsAsync(string solutionPath, AnalysisRequest request)
    {
        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(request, CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));

        var structural = CanonicalJson.Read<StructuralFactsShard>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == "facts/structural.json").Payload.AsSpan());
        return structural.Documents.Select(document => document.RelativePath).ToHashSet(StringComparer.Ordinal);
    }
}
