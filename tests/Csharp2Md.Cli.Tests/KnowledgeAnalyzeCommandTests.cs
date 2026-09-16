using System.CommandLine;
using System.CommandLine.Help;
using Csharp2Md.Cli;
using Csharp2Md.Core;

namespace Csharp2Md.Cli.Tests;

public sealed class KnowledgeAnalyzeCommandTests
{
    [Fact]
    public void Analyze_ExposesOnlyCurrentPackageOptions()
    {
        var analyze = AnalyzeCommand();

        var names = analyze.Options
            .Where(static option => option is not HelpOption and not VersionOption)
            .Select(static option => option.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["--include-tests", "--output", "--solution"], names);
        Assert.Empty(analyze.Arguments);
    }

    [Fact]
    public void Analyze_RequiresRepeatableSolutionsAndOneOutput()
    {
        var analyze = AnalyzeCommand();
        var solution = Assert.Single(analyze.Options, static option => option.Name == "--solution");
        var output = Assert.Single(analyze.Options, static option => option.Name == "--output");

        Assert.True(solution.Required);
        Assert.Equal(ArgumentArity.OneOrMore, solution.Arity);
        Assert.True(output.Required);
        Assert.Equal(ArgumentArity.ExactlyOne, output.Arity);
    }

    [Fact]
    public async Task Analyze_WithoutIncludeTests_ForwardsDisabledPolicy()
    {
        var recorder = new AnalyzeRecorder(Committed());
        var solution = OrdersSolution();
        var output = CliTestPaths.UniqueOutputPath();

        var result = await CliInvoke.RunAsync(
            ["analyze", "--solution", solution, "--output", output],
            recorder.InvokeAsync);

        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(recorder.Requests);
        Assert.False(request.IncludeTests);
        Assert.Equal(Path.GetFullPath(output), request.OutputDirectory);
    }

    [Fact]
    public async Task Analyze_WithIncludeTests_ForwardsEnabledPolicy()
    {
        var recorder = new AnalyzeRecorder(Committed());

        var result = await CliInvoke.RunAsync(
            ["analyze", "--solution", OrdersSolution(), "--output", CliTestPaths.UniqueOutputPath(), "--include-tests"],
            recorder.InvokeAsync);

        Assert.Equal(0, result.ExitCode);
        Assert.True(Assert.Single(recorder.Requests).IncludeTests);
    }

    [Fact]
    public async Task Analyze_WithTwoSolutions_InvokesCoreOnceWithBothInOrder()
    {
        var recorder = new AnalyzeRecorder(Committed());
        var orders = OrdersSolution();
        var payments = PaymentsSolution();

        var result = await CliInvoke.RunAsync(
            [
                "analyze",
                "--solution", orders,
                "--solution", payments,
                "--output", CliTestPaths.UniqueOutputPath(),
            ],
            recorder.InvokeAsync);

        Assert.Equal(0, result.ExitCode);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(
            string.Join(Environment.NewLine, orders, payments),
            string.Join(Environment.NewLine, request.SolutionPaths));
    }

    [Fact]
    public async Task Analyze_WhenCommitted_ReportsSuccessAfterCoreReturns()
    {
        var recorder = new AnalyzeRecorder(Committed());
        var output = CliTestPaths.UniqueOutputPath();

        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", OrdersSolution(), "--output", output],
            recorder.InvokeAsync);

        Assert.Equal(0, exitCode);
        Assert.Contains("committed and certified", stdout, StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(output), stdout, StringComparison.Ordinal);
        Assert.Equal(string.Empty, stderr);
        Assert.Single(recorder.Requests);
    }

    [Fact]
    public async Task Analyze_WhenCoreRejects_ReturnsNonZeroWithoutSuccessMessage()
    {
        var recorder = new AnalyzeRecorder(Rejected(new EngineDiagnostic(
            "package-budget", "package-building", "file-limit")));

        var (exitCode, stdout, _) = await CliInvoke.RunAsync(
            ["analyze", "--solution", OrdersSolution(), "--output", CliTestPaths.UniqueOutputPath()],
            recorder.InvokeAsync);

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain("committed", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Single(recorder.Requests);
    }

    [Theory]
    [InlineData("invocation", ExitCodes.InvalidInvocation)]
    [InlineData("certification", ExitCodes.CertificationFailed)]
    [InlineData("analysis", ExitCodes.StructuralCorruption)]
    public async Task Analyze_ForEveryRejectionClass_ReturnsNonZero(
        string stage,
        int expectedExitCode)
    {
        var recorder = new AnalyzeRecorder(Rejected(new EngineDiagnostic(
            "request-rejected", stage, "specified-cause")));

        var (exitCode, stdout, _) = await CliInvoke.RunAsync(
            ["analyze", "--solution", OrdersSolution(), "--output", CliTestPaths.UniqueOutputPath()],
            recorder.InvokeAsync);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
    }

    [Fact]
    public async Task Analyze_WhenCoreRejects_PrintsCodeStageAndCause()
    {
        var recorder = new AnalyzeRecorder(Rejected(new EngineDiagnostic(
            "variant-plan", "analysis", "missing-target-framework")));

        var (_, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", OrdersSolution(), "--output", CliTestPaths.UniqueOutputPath()],
            recorder.InvokeAsync);

        Assert.Single(stderr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("code=variant-plan", stderr, StringComparison.Ordinal);
        Assert.Contains("stage=analysis", stderr, StringComparison.Ordinal);
        Assert.Contains("cause=missing-target-framework", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_WhenDiagnosticHasCoordinates_PrintsEveryApplicableCoordinate()
    {
        var diagnostic = new EngineDiagnostic(
            "publication-rejected",
            "publication",
            "unsafe-path",
            solution: "orders",
            project: "src/Orders.csproj",
            variant: "net10.0",
            family: "source",
            artifact: "source/0.cs");
        var recorder = new AnalyzeRecorder(Rejected(diagnostic));

        var (_, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", OrdersSolution(), "--output", CliTestPaths.UniqueOutputPath()],
            recorder.InvokeAsync);

        Assert.Single(stderr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("code=publication-rejected", stderr, StringComparison.Ordinal);
        Assert.Contains("stage=publication", stderr, StringComparison.Ordinal);
        Assert.Contains("cause=unsafe-path", stderr, StringComparison.Ordinal);
        Assert.Contains("solution=orders", stderr, StringComparison.Ordinal);
        Assert.Contains("project=src/Orders.csproj", stderr, StringComparison.Ordinal);
        Assert.Contains("variant=net10.0", stderr, StringComparison.Ordinal);
        Assert.Contains("family=source", stderr, StringComparison.Ordinal);
        Assert.Contains("artifact=source/0.cs", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_WithoutSolution_IsRejectedBeforeCore()
    {
        var recorder = new AnalyzeRecorder(Committed());

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--output", CliTestPaths.UniqueOutputPath()],
            recorder.InvokeAsync);

        Assert.Equal(ExitCodes.InvalidInvocation, exitCode);
        Assert.Contains("--solution", stderr, StringComparison.Ordinal);
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task Analyze_WithoutOutput_IsRejectedBeforeCore()
    {
        var recorder = new AnalyzeRecorder(Committed());

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", OrdersSolution()],
            recorder.InvokeAsync);

        Assert.Equal(ExitCodes.InvalidInvocation, exitCode);
        Assert.Contains("--output", stderr, StringComparison.Ordinal);
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task Analyze_WithMissingSolution_IsRejectedBeforeCore()
    {
        var recorder = new AnalyzeRecorder(Committed());
        var missing = TempPath.Unique("csharp2md-missing-solution-") + ".slnx";

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", missing, "--output", CliTestPaths.UniqueOutputPath()],
            recorder.InvokeAsync);

        Assert.Equal(ExitCodes.InvalidInvocation, exitCode);
        Assert.Contains(Path.GetFullPath(missing), stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task Analyze_WithDuplicateSolution_IsRejectedBeforeCore()
    {
        var recorder = new AnalyzeRecorder(Committed());
        var solution = OrdersSolution();

        var (exitCode, _, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", solution, "--solution", solution, "--output", CliTestPaths.UniqueOutputPath()],
            recorder.InvokeAsync);

        Assert.Equal(ExitCodes.InvalidInvocation, exitCode);
        Assert.Contains("specified more than once", stderr, StringComparison.Ordinal);
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task Analyze_WhenOutputIsAFile_IsRejectedBeforeCore()
    {
        var recorder = new AnalyzeRecorder(Committed());
        var output = TempPath.Unique("csharp2md-output-file-");
        await File.WriteAllTextAsync(output, "occupied");
        try
        {
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", OrdersSolution(), "--output", output],
                recorder.InvokeAsync);

            Assert.Equal(ExitCodes.InvalidInvocation, exitCode);
            Assert.Contains("output path is a file", stderr, StringComparison.Ordinal);
            Assert.Empty(recorder.Requests);
        }
        finally
        {
            TempPath.TryDelete(output);
        }
    }

    private static Command AnalyzeCommand() =>
        Assert.Single(CommandFactory.CreateRootCommand().Subcommands, static command => command.Name == "analyze");

    private static AnalyzeResult Committed() => new(committed: true, diagnostics: []);

    private static AnalyzeResult Rejected(params EngineDiagnostic[] diagnostics) =>
        new(committed: false, [.. diagnostics]);

    private static string OrdersSolution() => FixtureSolution("Acme.Orders");

    private static string PaymentsSolution() => FixtureSolution("Acme.Payments");

    private static string FixtureSolution(string name)
    {
        var path = Path.Combine(
            CliTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            name,
            name + ".slnx");
        Assert.True(File.Exists(path), $"Fixture solution was not found at '{path}'.");
        return path;
    }

    private sealed class AnalyzeRecorder
    {
        private readonly AnalyzeResult _result;

        internal List<AnalyzeRequest> Requests { get; } = [];

        internal AnalyzeRecorder(AnalyzeResult result) => _result = result;

        internal Task<AnalyzeResult> InvokeAsync(AnalyzeRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_result);
        }
    }
}
