using Csharp2Md.Analysis;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeExitCodeTests
{
    [Fact]
    [Trait("Requirement", "ENG-42")]
    public async Task Analyze_WhenAnySolutionIsUnpublished_Exits2()
    {
        var solutionPath = ExistingFixturePath();
        IAnalysisEngine engine = new FakeAnalysisEngine(new AnalysisResult(
        [
            new SolutionOutcome(
                solutionPath,
                solutionPath.Replace('\\', '/'),
                PublicationStatus.Unpublished,
                failingStage: "Inventory",
                structuralCorruption: true,
                hasUnknownsOrCandidatesOrFrontiers: false,
                stages: []),
        ]));

        var (exitCode, _, _) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Requirement", "ENG-43")]
    public async Task Analyze_WhenOnlyUnknownsAreReported_Exits0()
    {
        var solutionPath = ExistingFixturePath();
        IAnalysisEngine engine = new FakeAnalysisEngine(new AnalysisResult(
        [
            new SolutionOutcome(
                solutionPath,
                solutionPath.Replace('\\', '/'),
                PublicationStatus.Committed,
                failingStage: null,
                structuralCorruption: false,
                hasUnknownsOrCandidatesOrFrontiers: true,
                stages: []),
        ]));

        var (exitCode, _, _) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    [Trait("Requirement", "STOR-47")]
    public async Task Analyze_WithInjectedEngine_StillRequiresOutputAndDoesNotWrite()
    {
        var solutionPath = ExistingFixturePath();
        var outputPath = CliTestPaths.UniqueOutputPath();
        IAnalysisEngine engine = new FakeAnalysisEngine(new AnalysisResult(
        [
            new SolutionOutcome(
                solutionPath,
                solutionPath.Replace('\\', '/'),
                PublicationStatus.Committed,
                failingStage: null,
                structuralCorruption: false,
                hasUnknownsOrCandidatesOrFrontiers: false,
                stages: []),
        ]));

        var missing = await CliInvoke.RunAsync(["analyze", "--solution", solutionPath], engine);
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("--output", missing.Stderr, StringComparison.Ordinal);

        try
        {
            var (exitCode, _, _) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath],
                engine);

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(outputPath));
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    private static string ExistingFixturePath()
    {
        var solutionPath = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(Path.Exists(solutionPath), $"Fixture solution was not found at '{solutionPath}'.");
        return solutionPath;
    }

    private sealed class FakeAnalysisEngine : IAnalysisEngine
    {
        private readonly AnalysisResult _result;

        public FakeAnalysisEngine(AnalysisResult result)
        {
            _result = result;
        }

        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }
}
