using System.Text.Json;

namespace Csharp2Md.Core.Tests.Cli;

[Trait("Category", "Integration")]
public sealed class V3CliRoutingTests : IAsyncLifetime
{
    public Task InitializeAsync() => CliBinary.EnsureBuiltAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ZeroOptions_UsesSyntaxOnlyUntrustedAndProducesV3WithoutDotnetOnPath()
    {
        using var workspace = new CliWorkspace();

        var result = await ProcessRunner.RunAsync(
            CliBinary.ExecutablePath, $"--output \"{workspace.Output}\"", workspace.Input, CancellationToken.None,
            new Dictionary<string, string> { ["PATH"] = string.Empty });

        Assert.Equal(0, result.ExitCode);
        using var manifest = workspace.ReadManifest();
        Assert.Equal("syntax-only", manifest.RootElement.GetProperty("analysis").GetProperty("requested").GetString());
        Assert.Equal("untrusted", manifest.RootElement.GetProperty("trust").GetString());
        Assert.False(File.Exists(Path.Combine(workspace.Output, "raw", "dependencies.json")));
    }

    [Fact]
    public async Task ExplicitSyntaxOnly_ProducesDocumentFactsAndMarkdown()
    {
        using var workspace = new CliWorkspace();

        var result = await workspace.RunAsync("--analysis syntax-only");

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(workspace.Output, "raw", "facts", "document"), "*.json", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(workspace.Output, "raw", "codebase", "App.cs.md")));
    }

    [Fact]
    public async Task SemanticWithoutTrust_ExitsOneAndPreservesSentinel()
    {
        using var workspace = new CliWorkspace(withSentinel: true);

        var result = await workspace.RunAsync("--analysis semantic");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("requires trusted-solution", result.StandardError, StringComparison.Ordinal);
        workspace.AssertSentinel();
    }

    [Fact]
    public async Task GeneratorsWithoutTrustedSemanticMode_ExitOneAndPreserveSentinel()
    {
        using var workspace = new CliWorkspace(withSentinel: true);

        var result = await workspace.RunAsync("--include-source-generators");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Source generators require trusted semantic analysis", result.StandardError, StringComparison.Ordinal);
        workspace.AssertSentinel();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("not-a-duration")]
    public async Task InvalidTimeout_ExitsOneAndPreservesSentinel(string timeout)
    {
        using var workspace = new CliWorkspace(withSentinel: true);

        var result = await workspace.RunAsync($"--analysis-timeout {timeout}");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("positive duration", result.StandardError, StringComparison.Ordinal);
        workspace.AssertSentinel();
    }

    [Theory]
    [InlineData("--analysis heuristic", "--analysis")]
    [InlineData("--trust maybe", "--trust")]
    public async Task UnknownModeOrTrust_ExitsOneBeforeOutput(string arguments, string expected)
    {
        using var workspace = new CliWorkspace(withSentinel: true);

        var result = await workspace.RunAsync(arguments);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(expected, result.StandardError, StringComparison.Ordinal);
        workspace.AssertSentinel();
    }

    [Fact]
    public async Task TrustedSemanticRequest_RoutesThroughEngineAndRecordsSyntaxFallback()
    {
        using var workspace = new CliWorkspace();

        var result = await workspace.RunAsync("--analysis semantic --trust trusted-solution");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("syntax-only migration cut", result.StandardError, StringComparison.Ordinal);
        using var manifest = workspace.ReadManifest();
        Assert.Equal("semantic", manifest.RootElement.GetProperty("analysis").GetProperty("requested").GetString());
        Assert.Equal("syntax-only", manifest.RootElement.GetProperty("analysis").GetProperty("effective").GetString());
    }

    [Fact]
    public async Task StructuralFailure_ReturnsEngineExitCodeOne()
    {
        using var workspace = new CliWorkspace("class C { void M() { } void M() { } }");

        var result = await workspace.RunAsync(string.Empty);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("duplicate-id", result.StandardError, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(workspace.Output, "raw", "facts", "manifest.json")));
    }

    private sealed class CliWorkspace : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-v3-cli-").FullName;
        private readonly string? _sentinel;

        public string Input { get; }
        public string Output { get; }

        public CliWorkspace(string source = "class App { void Run() { } }", bool withSentinel = false)
        {
            Input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
            Output = Path.Combine(_root, "output");
            File.WriteAllText(Path.Combine(Input, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(Input, "App.cs"), source);
            if (withSentinel)
            {
                Directory.CreateDirectory(Output);
                _sentinel = Path.Combine(Output, "sentinel.bin");
                File.WriteAllBytes(_sentinel, [0, 1, 2, 255]);
            }
        }

        public Task<ProcessResult> RunAsync(string extraArguments) => ProcessRunner.RunAsync(
            CliBinary.ExecutablePath,
            $"\"{Input}\" --output \"{Output}\" {extraArguments}",
            _root,
            CancellationToken.None);

        public JsonDocument ReadManifest() => JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Output, "raw", "facts", "manifest.json")));

        public void AssertSentinel()
        {
            Assert.NotNull(_sentinel);
            Assert.Equal(new byte[] { 0, 1, 2, 255 }, File.ReadAllBytes(_sentinel));
            Assert.Equal([_sentinel], Directory.GetFiles(Output));
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
