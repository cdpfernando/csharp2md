namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeSuccessTests
{
    [Fact]
    [Trait("Requirement", "ENG-41")]
    [Trait("Requirement", "ENG-44")]
    public async Task Analyze_WithExistingSolution_Exits0WithSummaryOnStdout()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");

        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()]);

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout));
        Assert.Contains(solutionPath.Replace('\\', '/'), stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("csharp2md:", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("csharp2md:", stderr, StringComparison.Ordinal);
    }
}
