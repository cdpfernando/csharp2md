using System.Text.Json;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Cli;

/// <summary>
/// Closes the highest-risk unknown in design.md: does <c>PackAsTool</c> package the Roslyn
/// BuildHost payload correctly? Packs the CLI, installs it in an isolated tool path, and runs it from a
/// directory outside the repo — the packed/installed execution context is what's actually at risk
/// (dotnet/roslyn#76797), not <c>dotnet run</c> from inside the repo.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PackagingSmokeTests
{
    private static readonly string RepoRoot = TestPaths.RepoRoot;
    private static readonly string CliProjectPath = Path.Combine(RepoRoot, "src", "Csharp2Md.Cli");
    private static readonly string NupkgDirectory = Path.Combine(CliProjectPath, "nupkg");

    [Fact]
    public async Task PackedTool_RunFromOutsideRepo_LoadsFixtureSolutionAndReportsProjectCount()
    {
        var packResult = await ProcessRunner.RunAsync(
            "dotnet", $"pack \"{CliProjectPath}\" -c Release --no-restore", RepoRoot, CancellationToken.None);
        Assert.True(packResult.ExitCode == 0, $"dotnet pack failed:\n{packResult.StandardOutput}\n{packResult.StandardError}");
        // The package's own metadata is the v4 release marker (AD-007); a stale nupkg version
        // would mean a `dotnet tool update` consumer never learns the output contract changed.
        Assert.Single(Directory.GetFiles(NupkgDirectory, "csharp2md.4.0.0.nupkg"));

        var toolDirectory = Directory.CreateTempSubdirectory("csharp2md-smoke-tool-").FullName;
        var nugetConfig = Path.Combine(toolDirectory, "NuGet.Config");
        File.WriteAllText(nugetConfig,
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="packed-tool" value="{System.Security.SecurityElement.Escape(NupkgDirectory)}" />
              </packageSources>
            </configuration>
            """);
        var installResult = await ProcessRunner.RunAsync(
            "dotnet",
            $"tool install --tool-path \"{toolDirectory}\" --configfile \"{nugetConfig}\" csharp2md --version 4.0.0",
            RepoRoot,
            CancellationToken.None);
        Assert.True(
            installResult.ExitCode == 0,
            $"dotnet tool install failed:\n{installResult.StandardOutput}\n{installResult.StandardError}");

        // Direct-directory mode uses the automatic .sln heuristic and inventories all four
        // declared projects without invoking restore or the Roslyn BuildHost.
        var outputDirectory = Directory.CreateTempSubdirectory("csharp2md-smoke-").FullName;
        var runDirectory = Directory.CreateTempSubdirectory("csharp2md-smoke-cwd-").FullName;
        var inputDirectory = TestPaths.SyntheticSolution("Acme.Orders");

        var toolCommand = Path.Combine(toolDirectory, OperatingSystem.IsWindows() ? "csharp2md.exe" : "csharp2md");
        var runResult = await ProcessRunner.RunAsync(
            toolCommand,
            $"\"{inputDirectory}\" --output \"{outputDirectory}\"",
            runDirectory, // outside the repo entirely — proves the packed tool is self-contained
            CancellationToken.None);

        Assert.True(
            runResult.ExitCode == 0,
            $"packed tool run failed (exit {runResult.ExitCode}):\n{runResult.StandardOutput}\n{runResult.StandardError}");
        Assert.Contains("Analyzed 4 project(s)", runResult.StandardOutput, StringComparison.Ordinal);

        // The real packed/installed binary — not just the in-process test build — must emit the
        // v4 factual contract and the schema-2 compact retrieval index, with no v2 compatibility file.
        var rawRoot = TopicLayout.RawRoot(outputDirectory);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(rawRoot, "facts", "manifest.json")));
        using var indexManifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(rawRoot, "index", "manifest.json")));
        Assert.Equal(2, manifest.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("4.0.0", manifest.RootElement.GetProperty("tool_version").GetString());
        Assert.Equal(2, indexManifest.RootElement.GetProperty("schema_version").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(indexManifest.RootElement.GetProperty("analysis_run_id").GetString()));
        Assert.False(File.Exists(Path.Combine(rawRoot, "dependencies.json")));
    }
}
