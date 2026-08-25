namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeInvocationErrorTests
{
    [Fact]
    [Trait("Requirement", "ENG-39")]
    public async Task Analyze_WithoutSolutionOption_Exits1NamingTheOption()
    {
        var (exitCode, _, stderr) = await CliInvoke.RunAsync(["analyze"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--solution", stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-40")]
    public async Task Analyze_WithMissingSolutionPath_Exits1NamingThePath()
    {
        var missingPath = Path.Combine(CliTestPaths.RepoRoot, "definitely-missing-solution.slnx");
        Assert.False(Path.Exists(missingPath), $"Precondition failed: '{missingPath}' unexpectedly exists.");

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(["analyze", "--solution", missingPath]);

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
            ["analyze", "--solution", solutionPath, "--solution", solutionPath]);

        Assert.Equal(1, exitCode);
        Assert.Contains(Path.GetFullPath(solutionPath), stderr, StringComparison.OrdinalIgnoreCase);
    }
}
