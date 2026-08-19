using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

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
        // T46: the package's own metadata is the v3 release marker (AD-007); a stale nupkg version
        // would mean a `dotnet tool update` consumer never learns the output contract changed.
        Assert.NotEmpty(Directory.GetFiles(NupkgDirectory, "csharp2md.3.0.0.nupkg"));

        var installResult = await ProcessRunner.RunAsync(
            "dotnet",
            $"tool install --global --add-source \"{NupkgDirectory}\" {ToolCommand}",
            RepoRoot,
            CancellationToken.None);
        Assert.True(installResult.ExitCode == 0, $"dotnet tool install failed:\n{installResult.StandardOutput}\n{installResult.StandardError}");

        // Direct-directory mode uses the automatic .sln heuristic and inventories all four
        // declared projects without invoking restore or the Roslyn BuildHost.
        var outputDirectory = Directory.CreateTempSubdirectory("csharp2md-smoke-").FullName;
        var runDirectory = Directory.CreateTempSubdirectory("csharp2md-smoke-cwd-").FullName;
        var inputDirectory = TestPaths.SyntheticSolution("Acme.Orders");

        var runResult = await ProcessRunner.RunAsync(
            ToolCommand,
            $"\"{inputDirectory}\" --output \"{outputDirectory}\"",
            runDirectory, // outside the repo entirely — proves the packed tool is self-contained
            CancellationToken.None);

        Assert.True(
            runResult.ExitCode == 0,
            $"packed tool run failed (exit {runResult.ExitCode}):\n{runResult.StandardOutput}\n{runResult.StandardError}");
        Assert.Contains("Analyzed 4 project(s)", runResult.StandardOutput, StringComparison.Ordinal);

        // T46: the real packed/installed binary — not just the in-process test build — must emit the
        // v3 factual contract: schema version 2, a matching tool version, and no v2 compatibility file.
        var rawRoot = TopicLayout.RawRoot(outputDirectory);
        var manifest = File.ReadAllText(Path.Combine(rawRoot, "facts", "manifest.json"));
        Assert.Contains("\"schema_version\": 2", manifest, StringComparison.Ordinal);
        Assert.Contains("\"tool_version\": \"3.0.0\"", manifest, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(rawRoot, "dependencies.json")));
    }

    private static async Task UninstallIfPresentAsync() =>
        await ProcessRunner.RunAsync("dotnet", $"tool uninstall --global {ToolCommand}", RepoRoot, CancellationToken.None);
}
