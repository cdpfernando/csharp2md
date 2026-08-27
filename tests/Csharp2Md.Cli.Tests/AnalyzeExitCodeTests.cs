using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeExitCodeTests
{
    [Fact]
    [Trait("Requirement", "ENG-42")]
    [Trait("Requirement", "STOR-55")]
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
    [Trait("Requirement", "ROSE-02")]
    public async Task Analyze_WhenUnpublishedWithDetail_WritesDetailToStderr()
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
                stages: [],
                detail: "link.cs"),
        ]));

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("link.cs", stderr, StringComparison.Ordinal);
        Assert.Contains("csharp2md:", stderr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ENG-43")]
    [Trait("Requirement", "STOR-54")]
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

    [Fact]
    [Trait("Requirement", "STOR-54")]
    public async Task Analyze_WhenUnknownsAreReported_FilesystemAdapterStillWritesAPackage()
    {
        var solutionPath = ExistingFixturePath();
        IAnalysisEngine unknowns = new FakeAnalysisEngine(new AnalysisResult(
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

        var unknownsExit = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            unknowns);
        Assert.Equal(0, unknownsExit.ExitCode);

        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            IAnalysisEngine engine = new AnalysisEngine(new FilesystemTransactionalStore(outputPath));
            var (exitCode, _, _) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath],
                engine);

            Assert.Equal(0, exitCode);
            Assert.Equal(
                "manifest.json",
                Path.GetFileName(Assert.Single(
                    Directory.EnumerateFiles(outputPath, "manifest.json", SearchOption.AllDirectories))));
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-55")]
    public async Task Analyze_WhenCommitThrowsPublicationRejected_Exits2()
    {
        var solutionPath = ExistingFixturePath();
        IAnalysisEngine engine = new AnalysisEngine(new RejectingTransactionalStore());

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solutionPath, "--output", CliTestPaths.UniqueOutputPath()],
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("unpublished", stderr, StringComparison.Ordinal);
        Assert.Contains("structural corruption", stderr, StringComparison.Ordinal);
        Assert.Contains("schema: facts/structural.json", stderr, StringComparison.Ordinal);
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

    private sealed class RejectingTransactionalStore : ITransactionalStore
    {
        public IStoreSession Open(string solutionKey, ISourceDocumentReader sourceReader) => new Session();

        private sealed class Session : IStoreSession
        {
            public void Stage(FactualSnapshot snapshot)
            {
            }

            public CommittedPublication Commit() =>
                throw new PublicationRejectedException("schema", "facts/structural.json");

            public void Abort()
            {
            }
        }
    }
}
