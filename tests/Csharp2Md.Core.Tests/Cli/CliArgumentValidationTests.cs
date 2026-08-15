using Csharp2Md.Core.Tests.Pipeline;

namespace Csharp2Md.Core.Tests.Cli;

[Trait("Category", "Integration")]
public sealed class CliArgumentValidationTests : IAsyncLifetime
{
    // Matches whatever configuration this very test assembly was built under (bin/<Config>/net10.0/).
    private static readonly string Configuration =
        Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)))!;

    private static readonly string CliProjectPath = Path.Combine(TestPaths.RepoRoot, "src", "Csharp2Md.Cli");

    private static readonly string CliDllPath = Path.Combine(
        CliProjectPath, "bin", Configuration, "net10.0", "Csharp2Md.Cli.dll");

    // `dotnet test` from the repo root only builds test projects and their own references — it does
    // not build sibling non-test projects like Csharp2Md.Cli. Build it explicitly (incremental/no-op
    // if already current) so this test class is self-contained under the Full gate (`dotnet test`).
    public async Task InitializeAsync()
    {
        var buildResult = await ProcessRunner.RunAsync(
            "dotnet", $"build \"{CliProjectPath}\" -c {Configuration}", TestPaths.RepoRoot, CancellationToken.None);
        Assert.True(buildResult.ExitCode == 0, $"dotnet build failed:\n{buildResult.StandardOutput}\n{buildResult.StandardError}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Run_WithNoArguments_PrintsUsageAndExitsNonZero()
    {
        var result = await ProcessRunner.RunAsync("dotnet", $"\"{CliDllPath}\"", TestPaths.RepoRoot, CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--manifest", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--output", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_MissingOutputOption_ExitsNonZero()
    {
        var manifestPath = TestPaths.SyntheticSolution(Path.Combine("Acme.Orders", "Acme.Orders.slnx"));

        var result = await ProcessRunner.RunAsync(
            "dotnet", $"\"{CliDllPath}\" --manifest \"{manifestPath}\"", TestPaths.RepoRoot, CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Run_MissingManifestOption_ExitsNonZero()
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet", $"\"{CliDllPath}\" --output \".\"", TestPaths.RepoRoot, CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Run_WithValidArguments_ExitsZeroAndReportsProjectCount()
    {
        // --manifest takes a manifest.json (P1-16), not a solution path directly — the automatic
        // .sln heuristic (P1-02) picks up Acme.Orders.slnx, which loads 3 real projects (Acme.Orders,
        // Acme.Shared.Contracts, Acme.Broken); the 4th reference, Acme.DoesNotExist, is skipped by
        // SkipUnrecognizedProjects (P1-06) rather than counted.
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-args-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteRoots(workspace, "Acme.Orders");
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                "dotnet",
                $"\"{CliDllPath}\" --manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot,
                CancellationToken.None);

            // Acme.Broken (referenced by Acme.Orders.slnx) is genuinely unrestorable, so the run
            // reports it needing attention rather than a clean summary — still exits 0 per P3-05.
            // Pinned to "need attention" specifically (not just "3 project(s)", which the clean-run
            // branch would also match) so this test actually exercises the degraded case it claims to.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("1 of 3 project(s) need attention", result.StandardOutput);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
