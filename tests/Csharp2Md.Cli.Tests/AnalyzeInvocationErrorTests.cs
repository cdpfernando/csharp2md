using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeInvocationErrorTests
{
    [Fact]
    [Trait("Requirement", "ENG-39")]
    public async Task Analyze_WithoutSolutionOption_Exits1NamingTheOption()
    {
        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--output", CliTestPaths.UniqueOutputPath()]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--solution", stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "STOR-48")]
    public async Task Analyze_WithoutOutputOption_Exits1NamingTheOption()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(["analyze", "--solution", solutionPath]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--output", stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-40")]
    public async Task Analyze_WithMissingSolutionPath_Exits1NamingThePath()
    {
        var missingPath = Path.Combine(CliTestPaths.RepoRoot, "definitely-missing-solution.slnx");
        Assert.False(Path.Exists(missingPath), $"Precondition failed: '{missingPath}' unexpectedly exists.");

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", missingPath, "--output", CliTestPaths.UniqueOutputPath()]);

        Assert.Equal(1, exitCode);
        Assert.Contains(missingPath, stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-40")]
    public async Task Analyze_WithDuplicateSolutionPaths_Exits1NamingTheDuplicate()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()]);

        Assert.Equal(1, exitCode);
        Assert.Contains(Path.GetFullPath(solutionPath), stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "ROSE-08")]
    public async Task Analyze_TwoFoldersSharingTheSameSolutionFileName_Exits1NamingBothPaths()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-cli-duplicate-id-");
        var output = CliTestPaths.UniqueOutputPath();
        try
        {
            var first = Path.Combine(tree.FullName, "left", "Acme.Orders.slnx");
            var second = Path.Combine(tree.FullName, "right", "Acme.Orders.slnx");
            Directory.CreateDirectory(Path.GetDirectoryName(first)!);
            Directory.CreateDirectory(Path.GetDirectoryName(second)!);
            File.WriteAllText(first, "<Solution />");
            File.WriteAllText(second, "<Solution />");

            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", first, "--solution", second, "--output", output],
                new AnalysisEngine(new OpenMustNotBeCalledStore()));

            Assert.Equal(1, exitCode);
            Assert.Contains(first, stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(second, stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            tree.Delete(recursive: true);
            CliTestPaths.TryDeleteDirectory(output);
        }
    }

    private sealed class OpenMustNotBeCalledStore : ITransactionalStore
    {
        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader) =>
            throw new InvalidOperationException($"Open must not be called, but was called with '{solutionKey}'.");
    }
}
