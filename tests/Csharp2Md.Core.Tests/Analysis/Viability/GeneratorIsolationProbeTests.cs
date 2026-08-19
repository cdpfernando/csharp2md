using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit.Abstractions;

namespace Csharp2Md.Core.Tests.Analysis.Viability;

/// <summary>
/// Generator-only adapter evidence based on the official Roslyn generator-driver contract:
/// https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.generatordriver.rungeneratorsandupdatecompilation
/// and https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md.
/// Roslyn documents that one assembly can mix analyzers and generators, so the adapter loads only
/// inventoried generator assemblies after explicit trusted opt-in.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GeneratorIsolationProbeTests(ITestOutputHelper output)
{
    [Fact]
    public void DisabledMode_ConstructsEvaluatedCompilationWithoutLoadingInventoriedExtensions()
    {
        using var fixture = GeneratorFixture.Create();
        fixture.CreateMarkerExtensions();

        var result = GeneratorIsolationProbe.Run(
            fixture.Inventory,
            trustedSolution: true,
            includeSourceGenerators: false);

        Assert.Empty(result.LoadedExtensions);
        Assert.Empty(result.ExtensionDiagnostics);
        Assert.Equal("Probe", result.Compilation.GetTypeByMetadataName("Probe")!.Name);
        Assert.False(File.Exists(fixture.AnalyzerLoadedMarker));
        Assert.False(File.Exists(fixture.AnalyzerExecutedMarker));
        Assert.False(File.Exists(fixture.GeneratorLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorExecutedMarker));
        AssertMetrics(result.Metrics);
        WriteMetrics("disabled", result.Metrics);
    }

    [Fact]
    public void GeneratorOptInWithoutTrust_IsRejectedBeforeLoadingEitherExtension()
    {
        using var fixture = GeneratorFixture.Create();
        fixture.CreateMarkerExtensions();

        var exception = Assert.Throws<InvalidOperationException>(
            () => GeneratorIsolationProbe.Run(
                fixture.Inventory,
                trustedSolution: false,
                includeSourceGenerators: true));

        Assert.Equal("Generator execution requires trusted solution input.", exception.Message);
        Assert.False(File.Exists(fixture.AnalyzerLoadedMarker));
        Assert.False(File.Exists(fixture.AnalyzerExecutedMarker));
        Assert.False(File.Exists(fixture.GeneratorLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorExecutedMarker));
    }

    [Fact]
    public void RepositorySourceSet_DisabledModeRecordsMetricsWithoutLoadingExtensions()
    {
        using var fixture = GeneratorFixture.Create();
        fixture.CreateMarkerExtensions();
        var sourceRoot = Path.Combine(TestPaths.RepoRoot, "src");
        var sources = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => new SourceDocument(Path.GetRelativePath(sourceRoot, path), File.ReadAllText(path)))
            .ToArray();

        var result = GeneratorIsolationProbe.Run(
            fixture.Inventory,
            trustedSolution: true,
            includeSourceGenerators: false,
            sourceDocuments: sources);

        Assert.Equal(sources.Length, result.Compilation.SyntaxTrees.Count());
        Assert.Empty(result.LoadedExtensions);
        Assert.False(File.Exists(fixture.AnalyzerLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorLoadedMarker));
        AssertMetrics(result.Metrics);
        WriteMetrics("repository-disabled", result.Metrics);
    }

    [Fact]
    public void TrustedGeneratorOptIn_LoadsAndExecutesGeneratorOnlyAndRecordsDiagnostics()
    {
        using var fixture = GeneratorFixture.Create();
        fixture.CreateMarkerExtensions();

        var result = GeneratorIsolationProbe.Run(
            fixture.Inventory,
            trustedSolution: true,
            includeSourceGenerators: true);

        Assert.Equal(["MarkerGenerator"], result.LoadedExtensions);
        Assert.Equal(["GEN001"], result.ExtensionDiagnostics);
        Assert.Contains(result.Compilation.SyntaxTrees, tree => tree.FilePath.EndsWith("Generated.g.cs", StringComparison.Ordinal));
        Assert.True(File.Exists(fixture.GeneratorLoadedMarker));
        Assert.True(File.Exists(fixture.GeneratorExecutedMarker));
        Assert.False(File.Exists(fixture.AnalyzerLoadedMarker));
        Assert.False(File.Exists(fixture.AnalyzerExecutedMarker));
        AssertMetrics(result.Metrics);
        WriteMetrics("enabled", result.Metrics);
    }

    private static void AssertMetrics(ProbeMetrics metrics)
    {
        Assert.True(metrics.Elapsed >= TimeSpan.Zero);
        Assert.True(metrics.PeakWorkingSetBytes > 0);
        Assert.True(metrics.ProcessCountBefore > 0);
        Assert.True(metrics.ProcessCountAfter > 0);
        Assert.True(metrics.OutputVolumeBytes > 0);
    }

    private void WriteMetrics(string mode, ProbeMetrics metrics) =>
        output.WriteLine(
            "GENERATOR_PROBE mode={0} elapsed_ms={1:F3} peak_working_set_bytes={2} " +
            "process_count_before={3} process_count_after={4} output_volume_bytes={5}",
            mode,
            metrics.Elapsed.TotalMilliseconds,
            metrics.PeakWorkingSetBytes,
            metrics.ProcessCountBefore,
            metrics.ProcessCountAfter,
            metrics.OutputVolumeBytes);

    private sealed record ExtensionInventory(string AnalyzerPath, string GeneratorPath);

    private sealed record SourceDocument(string Path, string Content);

    private sealed record ProbeMetrics(
        TimeSpan Elapsed,
        long PeakWorkingSetBytes,
        int ProcessCountBefore,
        int ProcessCountAfter,
        long OutputVolumeBytes);

    private sealed record IsolationResult(
        Compilation Compilation,
        IReadOnlyList<string> LoadedExtensions,
        IReadOnlyList<string> ExtensionDiagnostics,
        ProbeMetrics Metrics);

    private static class GeneratorIsolationProbe
    {
        private const string Source = "public sealed class Probe;";

        public static IsolationResult Run(
            ExtensionInventory inventory,
            bool trustedSolution,
            bool includeSourceGenerators,
            IReadOnlyList<SourceDocument>? sourceDocuments = null)
        {
            if (includeSourceGenerators && !trustedSolution)
            {
                throw new InvalidOperationException("Generator execution requires trusted solution input.");
            }

            var processCountBefore = CountProcesses();
            var stopwatch = Stopwatch.StartNew();
            var sources = sourceDocuments ?? [new SourceDocument("Probe.cs", Source)];
            Compilation compilation = CreateCompilation(sources);
            IReadOnlyList<string> loadedExtensions = [];
            IReadOnlyList<string> diagnostics = [];
            long outputVolume = sources.Sum(source => (long)source.Content.Length);

            if (includeSourceGenerators)
            {
                var generatorRun = RunGeneratorPass(compilation, inventory.GeneratorPath);
                compilation = generatorRun.Compilation;
                loadedExtensions = generatorRun.LoadedExtensions;
                diagnostics = generatorRun.Diagnostics;
                outputVolume = generatorRun.OutputVolumeBytes;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            stopwatch.Stop();
            using var currentProcess = Process.GetCurrentProcess();
            var metrics = new ProbeMetrics(
                stopwatch.Elapsed,
                currentProcess.PeakWorkingSet64,
                processCountBefore,
                CountProcesses(),
                outputVolume);
            return new IsolationResult(compilation, loadedExtensions, diagnostics, metrics);
        }

        private static CSharpCompilation CreateCompilation(IReadOnlyList<SourceDocument> sources) =>
            CSharpCompilation.Create(
                "Evaluated.Probe",
                sources.Select(source => CSharpSyntaxTree.ParseText(source.Content, path: source.Path)),
                TrustedPlatformReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        private static GeneratorPassResult RunGeneratorPass(Compilation compilation, string generatorPath)
        {
            var loadContext = new GeneratorLoadContext();
            try
            {
                var generatorAssembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(generatorPath));
                var generators = generatorAssembly.GetTypes()
                    .Where(type => !type.IsAbstract && typeof(ISourceGenerator).IsAssignableFrom(type))
                    .Select(type => (ISourceGenerator)Activator.CreateInstance(type)!)
                    .ToArray();
                var loadedExtensions = generators.Select(generator => generator.GetType().FullName!).ToArray();

                GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
                driver = driver.RunGeneratorsAndUpdateCompilation(
                    compilation,
                    out var updatedCompilation,
                    out var driverDiagnostics,
                    CancellationToken.None);
                var runResult = driver.GetRunResult();
                var diagnostics = driverDiagnostics
                    .Concat(runResult.Diagnostics)
                    .Select(diagnostic => diagnostic.Id)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                var outputVolume = runResult.GeneratedTrees.Sum(tree => (long)tree.GetText().Length);
                return new GeneratorPassResult(updatedCompilation, loadedExtensions, diagnostics, outputVolume);
            }
            finally
            {
                loadContext.Unload();
            }
        }

        private static IReadOnlyList<MetadataReference> TrustedPlatformReferences() =>
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();

        private static int CountProcesses()
        {
            var processes = Process.GetProcesses();
            try
            {
                return processes.Length;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private sealed record GeneratorPassResult(
            Compilation Compilation,
            IReadOnlyList<string> LoadedExtensions,
            IReadOnlyList<string> Diagnostics,
            long OutputVolumeBytes);

        private sealed class GeneratorLoadContext() : AssemblyLoadContext(isCollectible: true)
        {
            protected override Assembly? Load(AssemblyName assemblyName) =>
                Default.Assemblies.FirstOrDefault(
                    assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));
        }
    }

    private sealed class GeneratorFixture : IDisposable
    {
        private GeneratorFixture(string root)
        {
            Root = root;
            AnalyzerLoadedMarker = Path.Combine(root, "analyzer-loaded.marker");
            AnalyzerExecutedMarker = Path.Combine(root, "analyzer-executed.marker");
            GeneratorLoadedMarker = Path.Combine(root, "generator-loaded.marker");
            GeneratorExecutedMarker = Path.Combine(root, "generator-executed.marker");
            Inventory = new ExtensionInventory(
                Path.Combine(root, "MarkerAnalyzer.dll"),
                Path.Combine(root, "MarkerGenerator.dll"));
        }

        public string Root { get; }

        public string AnalyzerLoadedMarker { get; }

        public string AnalyzerExecutedMarker { get; }

        public string GeneratorLoadedMarker { get; }

        public string GeneratorExecutedMarker { get; }

        public ExtensionInventory Inventory { get; }

        public static GeneratorFixture Create() => new(Directory.CreateTempSubdirectory("c2m-gen-").FullName);

        public void CreateMarkerExtensions()
        {
            EmitExtension(
                Inventory.AnalyzerPath,
                $$"""
                using System.Collections.Immutable;
                using System.IO;
                using System.Runtime.CompilerServices;
                using Microsoft.CodeAnalysis;
                using Microsoft.CodeAnalysis.Diagnostics;

                internal static class LoadMarker
                {
                    [ModuleInitializer]
                    internal static void Initialize() => File.WriteAllText(@"{{AnalyzerLoadedMarker}}", "loaded");
                }

                [DiagnosticAnalyzer(LanguageNames.CSharp)]
                public sealed class MarkerAnalyzer : DiagnosticAnalyzer
                {
                    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [];
                    public override void Initialize(AnalysisContext context) =>
                        File.WriteAllText(@"{{AnalyzerExecutedMarker}}", "executed");
                }
                """,
                "MarkerAnalyzer");
            EmitExtension(
                Inventory.GeneratorPath,
                $$"""
                using System.IO;
                using System.Runtime.CompilerServices;
                using Microsoft.CodeAnalysis;

                internal static class LoadMarker
                {
                    [ModuleInitializer]
                    internal static void Initialize() => File.WriteAllText(@"{{GeneratorLoadedMarker}}", "loaded");
                }

                [Generator]
                public sealed class MarkerGenerator : ISourceGenerator
                {
                    private static readonly DiagnosticDescriptor Descriptor = new(
                        "GEN001", "Marker", "Marker generator ran", "Probe", DiagnosticSeverity.Warning, true);

                    public void Initialize(GeneratorInitializationContext context) { }

                    public void Execute(GeneratorExecutionContext context)
                    {
                        File.WriteAllText(@"{{GeneratorExecutedMarker}}", "executed");
                        context.ReportDiagnostic(Diagnostic.Create(Descriptor, Location.None));
                        context.AddSource("Generated.g.cs", "internal sealed class Generated;");
                    }
                }
                """,
                "MarkerGenerator");
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static void EmitExtension(string outputPath, string source, string assemblyName)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToList();
            references.Add(MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location));
            var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(outputPath);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        }
    }
}
