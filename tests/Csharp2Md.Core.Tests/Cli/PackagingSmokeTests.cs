namespace Csharp2Md.Core.Tests.Cli;

/// <summary>
/// Closes the highest-risk unknown in design.md: does <c>PackAsTool</c> package the Roslyn
/// BuildHost payload correctly? Packs the CLI, installs it as a global tool, and runs it from a
/// directory outside the repo — the packed/installed execution context is what's actually at risk
/// (dotnet/roslyn#76797), not `dotnet run` from inside the repo.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PackagingSmokeTests : IAsyncLifetime
{
    private const string ToolCommand = "csharp2md";
    private static readonly string RepoRoot = TestPaths.RepoRoot;
    private static readonly string CliProjectPath = Path.Combine(RepoRoot, "src", "Csharp2Md.Cli");
    private static readonly string NupkgDirectory = Path.Combine(CliProjectPath, "nupkg");

    public Task InitializeAsync() => UninstallIfPresentAsync();

    public Task DisposeAsync() => UninstallIfPresentAsync();

    [Fact]
    public async Task PackedTool_RunFromOutsideRepo_LoadsFixtureSolutionAndReportsProjectCount()
    {
        var packResult = await ProcessRunner.RunAsync(
            "dotnet", $"pack \"{CliProjectPath}\" -c Release", RepoRoot, CancellationToken.None);
        Assert.True(packResult.ExitCode == 0, $"dotnet pack failed:\n{packResult.StandardOutput}\n{packResult.StandardError}");
        Assert.NotEmpty(Directory.GetFiles(NupkgDirectory, "*.nupkg"));

        var installResult = await ProcessRunner.RunAsync(
            "dotnet",
            $"tool install --global --add-source \"{NupkgDirectory}\" {ToolCommand}",
            RepoRoot,
            CancellationToken.None);
        Assert.True(installResult.ExitCode == 0, $"dotnet tool install failed:\n{installResult.StandardOutput}\n{installResult.StandardError}");

        var fixtureSolutionPath = Path.Combine(
            RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        var outputDirectory = Directory.CreateTempSubdirectory("csharp2md-smoke-").FullName;
        var runDirectory = Directory.CreateTempSubdirectory("csharp2md-smoke-cwd-").FullName;

        var runResult = await ProcessRunner.RunAsync(
            ToolCommand,
            $"--manifest \"{fixtureSolutionPath}\" --output \"{outputDirectory}\"",
            runDirectory, // outside the repo entirely — proves the packed tool is self-contained
            CancellationToken.None);

        Assert.True(
            runResult.ExitCode == 0,
            $"packed tool run failed (exit {runResult.ExitCode}):\n{runResult.StandardOutput}\n{runResult.StandardError}");
        Assert.Contains("Loaded 3 project(s).", runResult.StandardOutput);
    }

    private static async Task UninstallIfPresentAsync() =>
        await ProcessRunner.RunAsync("dotnet", $"tool uninstall --global {ToolCommand}", RepoRoot, CancellationToken.None);
}
