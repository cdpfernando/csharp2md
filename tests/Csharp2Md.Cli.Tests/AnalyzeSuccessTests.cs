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

        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath]);

            Assert.Equal(0, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(stdout));
            Assert.Contains(solutionPath.Replace('\\', '/'), stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("csharp2md:", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("csharp2md:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-14")]
    [Trait("Requirement", "STOR-47")]
    public async Task Analyze_WithoutInjectedEngine_WritesAPackageUnderOutput()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");

        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var (exitCode, _, _) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath]);

            Assert.Equal(0, exitCode);
            var manifest = Assert.Single(
                Directory.EnumerateFiles(outputPath, "manifest.json", SearchOption.AllDirectories));
            Assert.Equal("manifest.json", Path.GetFileName(manifest));
            Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(manifest), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }
}
