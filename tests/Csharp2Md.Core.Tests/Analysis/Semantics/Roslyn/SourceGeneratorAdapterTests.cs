using System.Text;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Facts.Identity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Analysis.Semantics.Roslyn;

[Trait("Category", "Integration")]
public sealed class SourceGeneratorAdapterTests
{
    [Fact]
    public void DisabledMode_ProducesNoExecutionRequestAndLoadsNoAssembly()
    {
        using var fixture = GeneratorFixture.Create();
        var semantic = CreateSemanticCompilation();

        var request = SourceGeneratorExecutionRequest.Create(
            ProjectId,
            semantic,
            [fixture.ExtensionPath],
            TrustedOptions(includeGenerators: false));

        Assert.Null(request);
        Assert.False(File.Exists(fixture.AssemblyLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorExecutedMarker));
        Assert.False(File.Exists(fixture.AnalyzerConstructedMarker));
    }

    [Fact]
    public void GeneratorOptInWithoutTrust_IsRejectedBeforeAssemblyLoading()
    {
        using var fixture = GeneratorFixture.Create();
        var options = TrustedOptions(includeGenerators: true) with { Trust = TrustMode.Untrusted };

        var exception = Assert.Throws<ArgumentException>(() => SourceGeneratorExecutionRequest.Create(
            ProjectId,
            CreateSemanticCompilation(),
            [fixture.ExtensionPath],
            options));

        Assert.Equal("Semantic analysis requires trusted-solution trust. (Parameter 'options')", exception.Message);
        Assert.False(File.Exists(fixture.AssemblyLoadedMarker));
        Assert.False(File.Exists(fixture.GeneratorExecutedMarker));
        Assert.False(File.Exists(fixture.AnalyzerConstructedMarker));
    }

    [Fact]
    public void TrustedOptIn_RunsGeneratorOnlyAndRecordsStableDocumentsDiagnosticsAndEvidence()
    {
        using var fixture = GeneratorFixture.Create();
        var semantic = CreateSemanticCompilation();
        var request = RequiredRequest(semantic, fixture.ExtensionPath);

        var result = new SourceGeneratorAdapter().Run(request, CancellationToken.None);

        Assert.True(File.Exists(fixture.AssemblyLoadedMarker));
        Assert.True(File.Exists(fixture.GeneratorExecutedMarker));
        Assert.False(File.Exists(fixture.AnalyzerConstructedMarker));
        Assert.Equal(["MarkerExtensions.MarkerGenerator"], result.LoadedExtensions.ToArray());
        Assert.Equal(2, result.GeneratedDocuments.Length);
        Assert.Equal(["First.g.cs", "Second.g.cs"], result.GeneratedDocuments
            .Select(document => Path.GetFileName(document.RelativePath)).ToArray());
        Assert.All(result.GeneratedDocuments, document =>
        {
            Assert.Equal(semantic.TargetId, document.TargetId);
            Assert.StartsWith(".generated/MarkerExtensions.MarkerGenerator/", document.RelativePath, StringComparison.Ordinal);
            Assert.Equal(DocumentFactId.Create(ProjectId, document.RelativePath), document.DocumentId);
        });
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "GEN001");
        var evidence = Assert.Single(diagnostic.Evidence);
        Assert.Equal(semantic.Documents[0].DocumentId, evidence.DocumentId);
        Assert.Equal("Probe.cs", evidence.RelativePath);
        Assert.Contains(result.Compilation!.SyntaxTrees, tree => tree.FilePath.EndsWith("First.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void FailingGenerator_RetainsPreGeneratorCompilationAndAddsScopedDiagnostic()
    {
        using var fixture = GeneratorFixture.Create(failingGenerator: true);
        var semantic = CreateSemanticCompilation();

        var result = new SourceGeneratorAdapter().Run(
            RequiredRequest(semantic, fixture.ExtensionPath),
            CancellationToken.None);

        Assert.NotNull(result.Compilation);
        Assert.Contains(result.Compilation.SyntaxTrees, tree => tree.FilePath == "Probe.cs");
        Assert.Empty(result.GeneratedDocuments);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "C2M-GEN-003"
            && diagnostic.ScopeId == semantic.TargetId.ToFactId());
    }

    [Fact]
    public void MissingGeneratorAssembly_RetainsCompilationAndReturnsDeterministicLoadDiagnostic()
    {
        var semantic = CreateSemanticCompilation();
        var missing = Path.Combine(Path.GetTempPath(), "missing-generator.dll");

        var result = new SourceGeneratorAdapter().Run(
            RequiredRequest(semantic, missing),
            CancellationToken.None);

        Assert.Same(semantic.Compilation, result.Compilation);
        Assert.Empty(result.GeneratedDocuments);
        Assert.Empty(result.LoadedExtensions);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-GEN-002", diagnostic.Code);
        Assert.Equal(semantic.TargetId.ToFactId(), diagnostic.ScopeId);
        Assert.Equal("missing-generator.dll:FileNotFoundException", diagnostic.Data.Single().Value);
    }

    [Fact]
    public void IncrementalGenerator_IsLoadedAndItsGeneratedDocumentUsesOriginalTypeIdentity()
    {
        using var fixture = GeneratorFixture.Create(incrementalGenerator: true);
        var semantic = CreateSemanticCompilation();

        var result = new SourceGeneratorAdapter().Run(
            RequiredRequest(semantic, fixture.ExtensionPath),
            CancellationToken.None);

        Assert.Equal(["MarkerExtensions.MarkerIncrementalGenerator"], result.LoadedExtensions.ToArray());
        var generated = Assert.Single(result.GeneratedDocuments);
        Assert.Equal("MarkerExtensions.MarkerIncrementalGenerator", generated.GeneratorName);
        Assert.EndsWith("/Incremental.g.cs", generated.RelativePath, StringComparison.Ordinal);
        Assert.Contains("IncrementalGenerated", generated.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void UnavailableCompilation_DoesNotLoadGeneratorAndReturnsTargetDiagnostic()
    {
        using var fixture = GeneratorFixture.Create();
        var targetId = TargetFactId.Create(ProjectId, "net10.0");
        var semantic = new SemanticCompilationResult(
            targetId,
            null,
            SemanticBindingStatus.Unavailable,
            [],
            [],
            []);

        var result = new SourceGeneratorAdapter().Run(
            RequiredRequest(semantic, fixture.ExtensionPath),
            CancellationToken.None);

        Assert.Null(result.Compilation);
        Assert.False(File.Exists(fixture.AssemblyLoadedMarker));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-GEN-001", diagnostic.Code);
        Assert.Equal(targetId.ToFactId(), diagnostic.ScopeId);
    }

    private static ProjectFactId ProjectId { get; } = ProjectFactId.Create("Probe.csproj");

    private static SourceGeneratorExecutionRequest RequiredRequest(
        SemanticCompilationResult semantic,
        string generatorPath) =>
        SourceGeneratorExecutionRequest.Create(
            ProjectId,
            semantic,
            [generatorPath],
            TrustedOptions(includeGenerators: true))!;

    private static AnalysisOptions TrustedOptions(bool includeGenerators) => new()
    {
        Mode = AnalysisMode.Semantic,
        Trust = TrustMode.TrustedSolution,
        IncludeSourceGenerators = includeGenerators,
    };

    private static SemanticCompilationResult CreateSemanticCompilation()
    {
        var targetId = TargetFactId.Create(ProjectId, "net10.0");
        var target = new EvaluatedTarget(
            targetId,
            "net10.0",
            Succeeded: true,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TargetFramework"] = "net10.0",
                ["AssemblyName"] = "GeneratorProbe",
                ["OutputType"] = "Library",
                ["DefineConstants"] = string.Empty,
                ["LangVersion"] = "preview",
                ["Nullable"] = "enable",
            }.ToImmutableDictionary(StringComparer.Ordinal),
            new Dictionary<string, ImmutableArray<EvaluatedItem>>(StringComparer.Ordinal)
            {
                ["Reference"] = [],
                ["Analyzer"] = [],
            }.ToImmutableDictionary(StringComparer.Ordinal),
            []);
        var document = new SemanticSourceDocument(
            DocumentFactId.Create(ProjectId, "Probe.cs"),
            "Probe.cs",
            "public sealed class Probe;");
        return new SemanticCompilationAdapter().CreateCompilation(
            new SemanticCompilationRequest(target, "GeneratorProbe", [document]),
            CancellationToken.None);
    }

    private sealed class GeneratorFixture : IDisposable
    {
        private GeneratorFixture(string root)
        {
            Root = root;
            AssemblyLoadedMarker = Path.Combine(root, "assembly-loaded.marker");
            AnalyzerConstructedMarker = Path.Combine(root, "analyzer-constructed.marker");
            GeneratorExecutedMarker = Path.Combine(root, "generator-executed.marker");
            ExtensionPath = Path.Combine(root, "MarkerExtensions.dll");
        }

        public string Root { get; }

        public string AssemblyLoadedMarker { get; }

        public string AnalyzerConstructedMarker { get; }

        public string GeneratorExecutedMarker { get; }

        public string ExtensionPath { get; }

        public static GeneratorFixture Create(
            bool failingGenerator = false,
            bool incrementalGenerator = false)
        {
            var fixture = new GeneratorFixture(Directory.CreateTempSubdirectory("c2m-generator-").FullName);
            fixture.Compile(failingGenerator, incrementalGenerator);
            return fixture;
        }

        public void Dispose()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Directory.Delete(Root, recursive: true);
        }

        private void Compile(bool failingGenerator, bool incrementalGenerator)
        {
            var generatorBody = failingGenerator
                ? "throw new InvalidOperationException(\"controlled failure\");"
                : $$"""
                    File.WriteAllText(@"{{Escape(GeneratorExecutedMarker)}}", "executed");
                    var tree = context.Compilation.SyntaxTrees.First();
                    var span = new TextSpan(0, 1);
                    context.ReportDiagnostic(Diagnostic.Create(Descriptor, Location.Create(tree, span)));
                    context.AddSource("Second.g.cs", SourceText.From("public sealed class SecondGenerated;", Encoding.UTF8));
                    context.AddSource("First.g.cs", SourceText.From("public sealed class FirstGenerated;", Encoding.UTF8));
                    """;
            var generatorType = incrementalGenerator
                ? """
                  [Generator]
                  public sealed class MarkerIncrementalGenerator : IIncrementalGenerator
                  {
                      public void Initialize(IncrementalGeneratorInitializationContext context) =>
                          context.RegisterPostInitializationOutput(output => output.AddSource(
                              "Incremental.g.cs",
                              SourceText.From("public sealed class IncrementalGenerated;", Encoding.UTF8)));
                  }
                  """
                : $$"""
                  [Generator]
                  public sealed class MarkerGenerator : ISourceGenerator
                  {
                      private static readonly DiagnosticDescriptor Descriptor = new(
                          "GEN001", "Generator", "Generator ran", "Generator", DiagnosticSeverity.Warning, true);
                      public void Initialize(GeneratorInitializationContext context) { }
                      public void Execute(GeneratorExecutionContext context)
                      {
                          {{generatorBody}}
                      }
                  }
                  """;
            var source = $$"""
                using System;
                using System.Collections.Immutable;
                using System.IO;
                using System.Linq;
                using System.Runtime.CompilerServices;
                using System.Text;
                using Microsoft.CodeAnalysis;
                using Microsoft.CodeAnalysis.Diagnostics;
                using Microsoft.CodeAnalysis.Text;

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

                {{generatorType}}
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
            using var stream = File.Create(ExtensionPath);
            var emit = compilation.Emit(stream);
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        }

        private static string Escape(string path) => path.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}
