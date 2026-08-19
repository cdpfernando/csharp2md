using Csharp2Md.Core.Manifests;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Cli;

[Trait("Category", "Integration")]
public sealed class CliArgumentValidationTests : IAsyncLifetime
{
    public Task InitializeAsync() => CliBinary.EnsureBuiltAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Run_WithNoArguments_AnalyzesCurrentDirectoryAndUsesSiblingOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-current-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var expectedOutput = Path.Combine(workspace, "src_md");

            var result = await RunAsync(string.Empty, input);

            Assert.Equal(0, result.ExitCode);
            AssertGenerated(expectedOutput);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithExplicitDirectory_UsesInputNamedSiblingOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-directory-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var expectedOutput = Path.Combine(workspace, "src_md");

            var result = await RunAsync($"\"{input}\"", workspace);

            Assert.Equal(0, result.ExitCode);
            AssertGenerated(expectedOutput);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithManifestOnly_UsesManifestDirectoryNamedSiblingOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-manifest-default-").FullName;
        try
        {
            var input = CreateProject(workspace, "service");
            var manifestDirectory = Directory.CreateDirectory(Path.Combine(workspace, "config")).FullName;
            var manifestPath = FixtureManifest.Write(
                manifestDirectory, new Manifest([new ManifestEntry(input)]));
            var expectedOutput = Path.Combine(workspace, "config_md");

            var result = await RunAsync($"--manifest \"{manifestPath}\"", workspace);

            Assert.Equal(0, result.ExitCode);
            AssertGenerated(expectedOutput);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithExplicitOutput_WritesToExactDirectoryWithoutAppendingInputName()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-output-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var output = Path.Combine(workspace, "docs");

            var result = await RunAsync($"\"{input}\" --output \"{output}\"", workspace);

            Assert.Equal(0, result.ExitCode);
            AssertGenerated(output);
            Assert.False(Directory.Exists(Path.Combine(output, "src_md")));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithDirectoryAndManifest_PrintsUsageAndWritesNoOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-conflict-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var manifestPath = FixtureManifest.Write(
                Path.Combine(workspace, "config"), new Manifest([new ManifestEntry(input)]));
            var output = Path.Combine(workspace, "output");

            var result = await RunAsync(
                $"\"{input}\" --manifest \"{manifestPath}\" --output \"{output}\"", workspace);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Usage:", result.StandardError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithMissingDirectory_IdentifiesItAndWritesNoOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-missing-").FullName;
        try
        {
            var missing = Path.Combine(workspace, "missing");
            var expectedOutput = Path.Combine(workspace, "missing_md");

            var result = await RunAsync($"\"{missing}\"", workspace);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(missing, result.StandardError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(expectedOutput));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithFileAsDirectory_IdentifiesItAndWritesNoOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-file-").FullName;
        try
        {
            var file = Path.Combine(workspace, "input.txt");
            File.WriteAllText(file, "not a directory");
            var expectedOutput = Path.Combine(workspace, "input.txt_md");

            var result = await RunAsync($"\"{file}\"", workspace);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(file, result.StandardError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(expectedOutput));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithFilesystemRootAndNoOutput_RequiresExplicitOutputWithoutWriting()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-root-").FullName;
        try
        {
            var filesystemRoot = Path.GetPathRoot(workspace)!;
            var rootArgument = Path.Combine(filesystemRoot, ".");

            var result = await RunAsync($"\"{rootArgument}\"", workspace);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("--output", result.StandardError, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFileSystemEntries(workspace));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithUnmarkedOutput_RefusesAndPreservesExistingContent()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-unmarked-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var output = Directory.CreateDirectory(Path.Combine(workspace, "docs")).FullName;
            var existing = Path.Combine(output, "keep.txt");
            File.WriteAllText(existing, "keep me");

            var result = await RunAsync($"\"{input}\" --output \"{output}\"", workspace);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("--force", result.StandardError, StringComparison.Ordinal);
            Assert.Equal("keep me", File.ReadAllText(existing));
            Assert.Equal([existing], Directory.GetFileSystemEntries(output));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithForce_ReplacesUnmarkedOutputWithoutPrompting()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-force-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var output = Directory.CreateDirectory(Path.Combine(workspace, "docs")).FullName;
            var existing = Path.Combine(output, "remove.txt");
            File.WriteAllText(existing, "remove me");

            var result = await RunAsync(
                $"\"{input}\" --output \"{output}\" --force", workspace);

            Assert.Equal(0, result.ExitCode);
            Assert.False(File.Exists(existing));
            AssertGenerated(output);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithForcedOutputEqualToInput_RefusesAndPreservesSource()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-same-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var source = Path.Combine(input, "Program.cs");

            var result = await RunAsync(
                $"\"{input}\" --output \"{input}\" --force", workspace);

            Assert.NotEqual(0, result.ExitCode);
            Assert.True(File.Exists(source));
            Assert.Contains("cannot equal or contain", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithForcedOutputAsInputAncestor_RefusesAndPreservesSource()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-ancestor-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var source = Path.Combine(input, "Program.cs");

            var result = await RunAsync(
                $"\"{input}\" --output \"{workspace}\" --force", workspace);

            Assert.NotEqual(0, result.ExitCode);
            Assert.True(File.Exists(source));
            Assert.Contains("cannot equal or contain", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithValidManifestAndExplicitOutput_ExitsZeroAndReportsProjectCount()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-manifest-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteRoots(workspace, "Acme.Orders");
            var outputPath = Path.Combine(workspace, "output");

            var result = await RunAsync(
                $"--manifest \"{manifestPath}\" --output \"{outputPath}\"", TestPaths.RepoRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Analyzed 4 project(s)", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithHelp_ListsTopicAndDomainOptions()
    {
        var result = await ProcessRunner.RunAsync(
            CliBinary.ExecutablePath, "--help", TestPaths.RepoRoot, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--topic", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Slug identifying the generated topic", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--domain", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Slug identifying the topic's domain", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_WithInvalidTopic_ExitsOneBeforeTouchingOutputDirectory()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-topic-invalid-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var output = Path.Combine(workspace, "docs");

            var result = await RunAsync($"\"{input}\" --output \"{output}\" --topic \"Not A Slug!\"", workspace);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Not A Slug!", result.StandardError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithoutTopicOrDomain_DerivesSlugFromInputDirectoryAndDefaultsDomain()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-topic-default-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var output = Path.Combine(workspace, "docs");

            var result = await RunAsync($"\"{input}\" --output \"{output}\"", workspace);

            Assert.Equal(0, result.ExitCode);
            var topicYaml = File.ReadAllText(Path.Combine(TopicLayout.RawRoot(output), "topic.yaml"));
            Assert.Contains("topic: src", topicYaml, StringComparison.Ordinal);
            Assert.Contains("domain: system-design", topicYaml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_OnSuccess_ReportsProjectAndDocumentCountsAndOutputTopicPath()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-summary-").FullName;
        try
        {
            var input = CreateProject(workspace, "src");
            var output = Path.Combine(workspace, "docs");

            var result = await RunAsync($"\"{input}\" --output \"{output}\" --topic acme-shop", workspace);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Analyzed 1 project(s) and 1 document(s)", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                $"Output topic path: {Path.GetFullPath(output)}", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Run_WithExplicitEmptyDomain_PreservesMetadataAndWritesLog()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-cli-frontmatter-fail-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteRoots(workspace, "Acme.Orders");
            var outputPath = Path.Combine(workspace, "output");

            var result = await RunAsync(
                $"--manifest \"{manifestPath}\" --output \"{outputPath}\" --domain \"\"", TestPaths.RepoRoot);

            Assert.Equal(0, result.ExitCode);

            var logPath = Path.Combine(TopicLayout.RawRoot(outputPath), "log.md");
            Assert.True(File.Exists(logPath));
            var topicYaml = File.ReadAllText(Path.Combine(TopicLayout.RawRoot(outputPath), "topic.yaml"));
            Assert.Contains("domain: \n", topicYaml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static Task<ProcessResult> RunAsync(string arguments, string workingDirectory) =>
        ProcessRunner.RunAsync(
            CliBinary.ExecutablePath, arguments, workingDirectory, CancellationToken.None);

    private static string CreateProject(string workspace, string directoryName)
    {
        var directory = Directory.CreateDirectory(Path.Combine(workspace, directoryName)).FullName;
        File.WriteAllText(
            Path.Combine(directory, directoryName + ".csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(directory, "Program.cs"), "public static class Program { public static void Main() { } }");
        return directory;
    }

    private static void AssertGenerated(string output)
    {
        Assert.True(File.Exists(Path.Combine(output, ".csharp2md-output")));
        Assert.True(File.Exists(Path.Combine(TopicLayout.RawRoot(output), "facts", "manifest.json")));
        Assert.Single(Directory.EnumerateFiles(TopicLayout.CodebaseRoot(output), "*.cs.md", SearchOption.AllDirectories));
    }
}
