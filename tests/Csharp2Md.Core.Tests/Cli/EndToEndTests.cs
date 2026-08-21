using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Cli;

/// <summary>
/// Runs the packaged CLI apphost against the synthetic fixture and makes spec.md's P1 and P2
/// Independent Tests executable (T26's own "Done when" requirement), plus the P1-16 and BuildHost
/// error-handling paths from design.md's Error Handling Strategy.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EndToEndTests : IAsyncLifetime
{
    public Task InitializeAsync() => CliBinary.EnsureBuiltAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Run_AgainstFixture_ExitsZeroAndWritesFullArtifactSet()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteOverrides(
                workspace, SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts);
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath, $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot, CancellationToken.None);

            Assert.True(result.ExitCode == 0, $"run failed (exit {result.ExitCode}):\n{result.StandardOutput}\n{result.StandardError}");

            var rawRoot = TopicLayout.RawRoot(outputPath);
            var codebaseRoot = TopicLayout.CodebaseRoot(outputPath);
            var generatedDocs = Directory
                .EnumerateFiles(codebaseRoot, "*.cs.md", SearchOption.AllDirectories)
                .ToArray();
            Assert.NotEmpty(generatedDocs);
            Assert.All(generatedDocs, path =>
                Assert.Contains("schema_version: 2", File.ReadAllText(path), StringComparison.Ordinal));

            Assert.True(File.Exists(Path.Combine(rawRoot, "facts", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(rawRoot, "facts", "relations", "compile-time.json")));
            Assert.True(File.Exists(Path.Combine(rawRoot, "dependencies.mmd")));
            Assert.True(File.Exists(Path.Combine(outputPath, ".csharp2md-output")));
            Assert.Contains("Analyzed 3 project(s)", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_AgainstFixture_WritesSyntaxOnlyRelationPartitionsWithoutV2Graph()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteOverrides(
                workspace, SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts);
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath, $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot, CancellationToken.None);
            Assert.True(result.ExitCode == 0, $"run failed (exit {result.ExitCode}):\n{result.StandardOutput}\n{result.StandardError}");

            var rawRoot = TopicLayout.RawRoot(outputPath);
            Assert.False(File.Exists(Path.Combine(rawRoot, "dependencies.json")));
            // The fixture produces inheritance, http and events relations in syntax-only mode; the
            // remaining partitions have no syntax-only producer. Every partition being empty was the
            // unwired-projector defect, not the contract.
            (string Partition, bool Populated)[] partitions =
            [
                ("compile-time", false), ("inheritance", true), ("dependency-injection", false),
                ("http", true), ("grpc", false), ("events", true),
            ];

            Assert.All(partitions, entry =>
            {
                var json = File.ReadAllText(Path.Combine(rawRoot, "facts", "relations", entry.Partition + ".json"));
                Assert.Contains($"\"kind\": \"{entry.Partition}\"", json, StringComparison.Ordinal);
                if (entry.Populated)
                {
                    Assert.Contains($"\"partition\": \"{entry.Partition}\"", json, StringComparison.Ordinal);
                }
                else
                {
                    Assert.Contains("\"entries\": []", json, StringComparison.Ordinal);
                }
            });

        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_InvalidManifest_ExitsNonZeroAndWritesNoOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-invalid-").FullName;
        try
        {
            var manifestPath = Path.Combine(workspace, "manifest.json");
            File.WriteAllText(manifestPath, "{ not valid json");
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath, $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot, CancellationToken.None);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("csharp2md:", result.StandardError, StringComparison.Ordinal); // P1-16: names the problem
            Assert.False(Directory.Exists(outputPath) && Directory.EnumerateFileSystemEntries(outputPath).Any());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_SyntaxOnlyWithoutDotnetOnPath_SucceedsWithoutExecutableAnalysis()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-e2e-nodotnet-").FullName;

        var pathWithoutDotnet = Directory.CreateTempSubdirectory("csharp2md-empty-path-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteOverrides(workspace, SyntheticFixtureRun.Orders);
            var outputPath = Path.Combine(workspace, "output");

            var result = await ProcessRunner.RunAsync(
                CliBinary.ExecutablePath,
                $"--manifest \"{manifestPath}\" --output \"{outputPath}\"",
                TestPaths.RepoRoot,
                CancellationToken.None,
                environment: new Dictionary<string, string> { ["PATH"] = pathWithoutDotnet, ["DOTNET_ROOT"] = pathWithoutDotnet });

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(outputPath, "raw", "facts", "manifest.json")));
            Assert.DoesNotContain("Unhandled exception", result.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("   at ", result.StandardError, StringComparison.Ordinal); // no raw stack trace
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
            Directory.Delete(pathWithoutDotnet, recursive: true);
        }
    }

}
