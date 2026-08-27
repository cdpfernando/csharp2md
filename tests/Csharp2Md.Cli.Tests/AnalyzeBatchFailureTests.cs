using System.CommandLine;
using System.CommandLine.Help;
using Csharp2Md.Analysis;
using Csharp2Md.Cli;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeBatchFailureTests
{
    [Fact]
    [Trait("Requirement", "MSC-15")]
    public async Task Analyze_WhenBatchPublicationFails_Exits2AndNamesTheReason()
    {
        var solutionPath = ExistingFixturePath();
        IAnalysisEngine engine = new FakeAnalysisEngine(new AnalysisResult(
            [Committed(solutionPath)],
            batchPublicationGate: "batch-composition",
            batchPublicationDetail: "root"));

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("csharp2md:", stderr, StringComparison.Ordinal);
        Assert.Contains("batch-composition", stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public async Task Analyze_WhenEverySolutionIsCommitted_Exits0()
    {
        var solutionPath = ExistingFixturePath();
        IAnalysisEngine engine = new FakeAnalysisEngine(new AnalysisResult([Committed(solutionPath)]));

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("batch-composition", stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public async Task Analyze_WhenASolutionIsUnpublished_Exits2()
    {
        var solutionPath = ExistingFixturePath();
        IAnalysisEngine engine = new FakeAnalysisEngine(new AnalysisResult(
        [
            new SolutionOutcome(
                solutionPath,
                solutionPath.Replace('\\', '/'),
                PublicationStatus.Unpublished,
                failingStage: "Inventory",
                structuralCorruption: false,
                hasUnknownsOrCandidatesOrFrontiers: false,
                stages: []),
        ]));

        var (exitCode, _, _) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public void AnalyzeAndRoot_ExposeNoNewOption()
    {
        var root = CommandFactory.CreateRootCommand();
        var analyze = Assert.Single(root.Subcommands);

        var analyzeProductOptions = analyze.Options
            .Where(static option => option is not HelpOption and not VersionOption)
            .Select(static option => option.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["--output", "--solution"], analyzeProductOptions);
        Assert.Empty(analyze.Arguments);
        Assert.DoesNotContain(
            root.Options,
            option => option is not HelpOption and not VersionOption
                && option.Name is not "--output" and not "--solution");
    }

    private static SolutionOutcome Committed(string solutionPath) =>
        new(
            solutionPath,
            solutionPath.Replace('\\', '/'),
            PublicationStatus.Committed,
            failingStage: null,
            structuralCorruption: false,
            hasUnknownsOrCandidatesOrFrontiers: false,
            stages: []);

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

        public FakeAnalysisEngine(AnalysisResult result) => _result = result;

        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }
}
