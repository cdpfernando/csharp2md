using System.Diagnostics;
using System.Text.Json;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Cli;

[Trait("Category", "Integration")]
public sealed class V3SecurityBoundaryTests : IAsyncLifetime
{
    public Task InitializeAsync() => CliBinary.EnsureBuiltAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DefaultSyntaxOnly_DoesNotInvokeDotnetShim()
    {
        using var workspace = new BoundaryWorkspace();
        var shim = workspace.CreateDotnetShim();
        var result = await workspace.RunAsync(string.Empty, shim.Environment);
        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(shim.InvocationMarker));
        workspace.AssertSyntaxArtifacts();
    }

    [Fact]
    public async Task ExplicitSyntaxOnly_DoesNotInvokeDotnetShim()
    {
        using var workspace = new BoundaryWorkspace();
        var shim = workspace.CreateDotnetShim();
        var result = await workspace.RunAsync("--analysis syntax-only", shim.Environment);
        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(shim.InvocationMarker));
        workspace.AssertSyntaxArtifacts();
    }

    [Fact]
    public async Task SemanticWithoutTrust_ExitsOneBeforeModifyingNestedSentinel()
    {
        using var workspace = new BoundaryWorkspace(withNestedSentinel: true);
        var result = await workspace.RunAsync("--analysis semantic");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("requires trusted-solution", result.StandardError, StringComparison.Ordinal);
        workspace.AssertSentinelUnchanged();
    }

    [Fact]
    public async Task GeneratorsWithoutTrustedSemanticMode_ExitOneBeforeModifyingNestedSentinel()
    {
        using var workspace = new BoundaryWorkspace(withNestedSentinel: true);
        var result = await workspace.RunAsync("--include-source-generators");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Source generators require trusted semantic analysis", result.StandardError, StringComparison.Ordinal);
        workspace.AssertSentinelUnchanged();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-00:00:01")]
    [InlineData("not-a-duration")]
    public async Task InvalidTimeout_ExitsOneBeforeModifyingNestedSentinel(string timeout)
    {
        using var workspace = new BoundaryWorkspace(withNestedSentinel: true);
        var result = await workspace.RunAsync($"--analysis-timeout {timeout}");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("positive duration", result.StandardError, StringComparison.Ordinal);
        workspace.AssertSentinelUnchanged();
    }

    [Fact]
    public async Task MissingSdkSemanticRun_RetainsSyntaxFactsAndRecordsFallbackContract()
    {
        using var workspace = new BoundaryWorkspace(project: "<Project Sdk=\"Csharp2Md.Intentionally.Missing.Sdk/99.0.0\" />");
        var result = await workspace.RunAsync("--analysis semantic --trust trusted-solution");
        Assert.Equal(0, result.ExitCode);
        workspace.AssertSyntaxArtifacts();
        Assert.Contains("C2M-EVAL-001", workspace.ReadDiagnostics(), StringComparison.Ordinal);
        Assert.Contains("\"attempt\": \"not-attempted\"", workspace.ReadCoverage(), StringComparison.Ordinal);
        using var manifest = workspace.ReadManifest();
        Assert.Equal("semantic", manifest.RootElement.GetProperty("analysis").GetProperty("requested").GetString());
        Assert.Equal("syntax-only", manifest.RootElement.GetProperty("analysis").GetProperty("effective").GetString());
        Assert.False(manifest.RootElement.GetProperty("restore_performed").GetBoolean());
        Assert.Equal("none", manifest.RootElement.GetProperty("isolation").GetString());
    }

    [Fact]
    public async Task TrustedSemanticWithoutGeneratorOptIn_LoadsNoExtensionAssembly()
    {
        using var extension = ExtensionFixture.Create();
        using var workspace = new BoundaryWorkspace(project: extension.ProjectXml);
        var result = await workspace.RunAsync("--analysis semantic --trust trusted-solution");
        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(extension.AssemblyLoadedMarker));
        Assert.False(File.Exists(extension.GeneratorExecutedMarker));
        Assert.False(File.Exists(extension.AnalyzerConstructedMarker));
    }

    [Fact]
    public async Task TrustedGeneratorOptIn_ExecutesGeneratorButNeverConstructsAnalyzer()
    {
        using var extension = ExtensionFixture.Create();
        using var workspace = new BoundaryWorkspace(project: extension.ProjectXml);
        var result = await workspace.RunAsync("--analysis semantic --trust trusted-solution --include-source-generators");
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(extension.AssemblyLoadedMarker));
        Assert.True(File.Exists(extension.GeneratorExecutedMarker));
        Assert.False(File.Exists(extension.AnalyzerConstructedMarker));
        using var manifest = workspace.ReadManifest();
        Assert.Contains("MarkerExtensions.MarkerGenerator", manifest.RootElement.GetProperty("extensions").EnumerateArray().Select(static value => value.GetString()));
    }

    [Fact]
    public async Task FailingTrustedGenerator_RetainsSyntaxArtifactsAndReturnsZero()
    {
        using var extension = ExtensionFixture.Create(failingGenerator: true);
        using var workspace = new BoundaryWorkspace(project: extension.ProjectXml);
        var result = await workspace.RunAsync("--analysis semantic --trust trusted-solution --include-source-generators");
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(extension.GeneratorExecutedMarker));
        Assert.Contains("C2M-GEN-003", workspace.ReadDiagnostics(), StringComparison.Ordinal);
        workspace.AssertSyntaxArtifacts();
    }

    [Fact]
    public async Task EvaluationProcessCancellation_KillsParentAndDescendantBeforeReturning()
    {
        using var fixture = new TimeoutProcessFixture();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var runner = new EvaluationProcessRunner();
        var running = runner.RunAsync(fixture.StartInfo, cancellation.Token);
        var ids = await fixture.ReadProcessIdsAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        Assert.False(IsRunning(ids.Parent));
        Assert.False(IsRunning(ids.Descendant));
        Assert.False(File.Exists(fixture.CompletionMarker));
    }

    [Fact]
    public async Task StructuralInvalidity_ExitsOneAfterPublishingDiagnostics()
    {
        using var workspace = new BoundaryWorkspace(source: "class C { void M() { } void M() { } }");
        var result = await workspace.RunAsync(string.Empty);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("duplicate-id", result.StandardError, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(workspace.Output, "raw", "facts", "manifest.json")));
        Assert.Contains("duplicate-id", workspace.ReadDiagnostics(), StringComparison.Ordinal);
    }

    private static bool IsRunning(int processId)
    {
        try { using var process = Process.GetProcessById(processId); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private sealed class BoundaryWorkspace : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-v3-boundary-").FullName;
        private readonly string? _sentinel;

        public BoundaryWorkspace(string source = "public sealed class App { public void Run() { } }", string? project = null, bool withNestedSentinel = false)
        {
            Input = Directory.CreateDirectory(Path.Combine(_root, "input")).FullName;
            Output = Path.Combine(_root, "output");
            File.WriteAllText(Path.Combine(Input, "App.csproj"), project ?? "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(Input, "App.cs"), source);
            if (withNestedSentinel)
            {
                var directory = Directory.CreateDirectory(Path.Combine(Output, "keep", "nested")).FullName;
                _sentinel = Path.Combine(directory, "sentinel.bin");
                File.WriteAllBytes(_sentinel, [0, 1, 2, 255]);
            }
        }

        public string Input { get; }
        public string Output { get; }
        public Task<ProcessResult> RunAsync(string arguments, IReadOnlyDictionary<string, string>? environment = null) =>
            ProcessRunner.RunAsync(CliBinary.ExecutablePath, $"\"{Input}\" --output \"{Output}\" {arguments}", _root, CancellationToken.None, environment);
        public JsonDocument ReadManifest() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "raw", "facts", "manifest.json")));
        public string ReadDiagnostics() => File.ReadAllText(Path.Combine(Output, "raw", "facts", "diagnostics.json"));
        public string ReadCoverage() => File.ReadAllText(Path.Combine(Output, "raw", "facts", "coverage.json"));

        public DotnetShim CreateDotnetShim()
        {
            var directory = Directory.CreateDirectory(Path.Combine(_root, "dotnet-shim")).FullName;
            var marker = Path.Combine(directory, "invoked.marker");
            File.WriteAllText(Path.Combine(directory, "dotnet.cmd"), $"@echo off{Environment.NewLine}echo invoked>> \"{marker}\"{Environment.NewLine}exit /b 1");
            return new DotnetShim(directory, marker);
        }

        public void AssertSyntaxArtifacts()
        {
            Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(Output, "raw", "facts", "document"), "*.json", SearchOption.AllDirectories));
            Assert.True(File.Exists(Path.Combine(Output, "raw", "codebase", "App.cs.md")));
        }

        public void AssertSentinelUnchanged()
        {
            Assert.NotNull(_sentinel);
            Assert.Equal(new byte[] { 0, 1, 2, 255 }, File.ReadAllBytes(_sentinel));
            Assert.Equal([_sentinel], Directory.GetFiles(Output, "*", SearchOption.AllDirectories));
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private sealed class DotnetShim(string directory, string? invocationMarker)
    {
        public IReadOnlyDictionary<string, string> Environment { get; } = new Dictionary<string, string>
        {
            ["PATH"] = directory + Path.PathSeparator + System.Environment.SystemDirectory,
        };
        public string? InvocationMarker { get; } = invocationMarker;
    }

    private sealed class TimeoutProcessFixture : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-v3-process-").FullName;

        public TimeoutProcessFixture()
        {
            ProcessIdsPath = Path.Combine(_root, "process-ids.txt");
            CompletionMarker = Path.Combine(_root, "completed.marker");
            var scriptPath = Path.Combine(_root, "timeout.ps1");
            var script = "$child = Start-Process -FilePath 'powershell.exe' "
                + "-ArgumentList '-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 300' "
                + $"-PassThru -WindowStyle Hidden{Environment.NewLine}"
                + $"Set-Content -LiteralPath '{Escape(ProcessIdsPath)}' -Value @($PID, $child.Id){Environment.NewLine}"
                + "Start-Sleep -Seconds 300" + Environment.NewLine
                + $"Set-Content -LiteralPath '{Escape(CompletionMarker)}' -Value 'completed'";
            File.WriteAllText(scriptPath, script);
            StartInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            StartInfo.ArgumentList.Add("-NoProfile");
            StartInfo.ArgumentList.Add("-NonInteractive");
            StartInfo.ArgumentList.Add("-File");
            StartInfo.ArgumentList.Add(scriptPath);
        }

        public ProcessStartInfo StartInfo { get; }
        public string ProcessIdsPath { get; }
        public string CompletionMarker { get; }

        public async Task<(int Parent, int Descendant)> ReadProcessIdsAsync()
        {
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(ProcessIdsPath) && deadline.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(25);
            var values = await File.ReadAllLinesAsync(ProcessIdsPath);
            Assert.Equal(2, values.Length);
            return (int.Parse(values[0]), int.Parse(values[1]));
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
        private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    }

    private sealed class ExtensionFixture : IDisposable
    {
        private ExtensionFixture(string root, bool failingGenerator)
        {
            Root = root;
            AssemblyPath = Path.Combine(root, "MarkerExtensions.dll");
            AssemblyLoadedMarker = Path.Combine(root, "assembly-loaded.marker");
            AnalyzerConstructedMarker = Path.Combine(root, "analyzer-constructed.marker");
            GeneratorExecutedMarker = Path.Combine(root, "generator-executed.marker");
            Compile(failingGenerator);
        }

        public string Root { get; }
        public string AssemblyPath { get; }
        public string AssemblyLoadedMarker { get; }
        public string AnalyzerConstructedMarker { get; }
        public string GeneratorExecutedMarker { get; }
        public string ProjectXml => $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><Analyzer Include="{{AssemblyPath}}" ExtensionKind="Generator" /></ItemGroup>
            </Project>
            """;

        public static ExtensionFixture Create(bool failingGenerator = false) =>
            new(Directory.CreateTempSubdirectory("csharp2md-v3-extension-").FullName, failingGenerator);

        public void Dispose()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Directory.Delete(Root, recursive: true);
        }

        private void Compile(bool failingGenerator)
        {
            var body = failingGenerator
                ? $$"""File.WriteAllText(@"{{Escape(GeneratorExecutedMarker)}}", "executed"); throw new InvalidOperationException("controlled failure");"""
                : $$"""File.WriteAllText(@"{{Escape(GeneratorExecutedMarker)}}", "executed"); context.AddSource("Generated.g.cs", "internal sealed class Generated;");""";
            var source = $$"""
                using System;
                using System.Collections.Immutable;
                using System.IO;
                using System.Runtime.CompilerServices;
                using Microsoft.CodeAnalysis;
                using Microsoft.CodeAnalysis.Diagnostics;
                namespace MarkerExtensions;
                internal static class LoadMarker
                {
                    [ModuleInitializer]
                    internal static void Initialize() => File.WriteAllText(@"{{Escape(AssemblyLoadedMarker)}}", "loaded");
                }
                [DiagnosticAnalyzer(LanguageNames.CSharp)]
                public sealed class MarkerAnalyzer : DiagnosticAnalyzer
                {
                    public MarkerAnalyzer() => File.WriteAllText(@"{{Escape(AnalyzerConstructedMarker)}}", "constructed");
                    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [];
                    public override void Initialize(AnalysisContext context) { }
                }
                [Generator]
                public sealed class MarkerGenerator : ISourceGenerator
                {
                    public void Initialize(GeneratorInitializationContext context) { }
                    public void Execute(GeneratorExecutionContext context) { {{body}} }
                }
                """;
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Append(typeof(ISourceGenerator).Assembly.Location)
                .Append(typeof(CSharpCompilation).Assembly.Location)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(static path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                "MarkerExtensions",
                [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = File.Create(AssemblyPath);
            var emitted = compilation.Emit(stream);
            Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        }

        private static string Escape(string path) => path.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}
