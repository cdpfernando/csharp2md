using Csharp2Md.Analysis.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Csharp2Md.Analysis.Tests.Semantics;

/// <summary>
/// GCPC-085 (AD-003): a behavioral regression guard proving source generators still require separate
/// consent -- none exists in this codebase today, so they must never run at all -- and diagnostic
/// analyzers never execute. <see cref="CompilationSanitizerTests"/> (ROSE-29/30) already proves no
/// analyzer or generator *reference* survives <see cref="CompilationSanitizer.Strip"/>; this file adds
/// the complementary behavioral proof with a canary that actually flips a flag when it runs, reusing
/// the same real workspace construction (<see cref="MsBuildWorkspaceFactory"/>) and
/// <see cref="CompilationSanitizer"/> the production pipeline (<c>SemanticAnalysisStage</c>) itself
/// calls.
/// </summary>
public sealed class TrustBoundaryTests
{
    [Fact]
    [Trait("Requirement", "GCPC-085")]
    public async Task Strip_ThenGetCompilationAsync_NeverExecutesTheAttachedSourceGenerator()
    {
        var generator = new CanaryGenerator();
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Probe", LanguageNames.CSharp)
            .AddDocument("Probe.cs", "class Probe;")
            .Project
            .WithAnalyzerReferences([new CanaryGeneratorReference(generator)]);

        // Proof of life: the SAME project, not yet stripped, really does run the generator when
        // compiled -- proving the canary is genuinely wired into Roslyn's own generator-execution path.
        // This is also the guard's own self-check for GCPC-085's "fails if the consent flag is removed
        // or defaulted on": if CompilationSanitizer.Strip were ever skipped in the real pipeline, this
        // is exactly the outcome that skip would produce.
        _ = await project.GetCompilationAsync();
        Assert.True(generator.Executed, "Proof-of-life failed: the canary generator never ran even without stripping.");

        generator.Reset();
        var stripped = CompilationSanitizer.Strip(project);
        var compilation = await stripped.GetCompilationAsync();

        Assert.NotNull(compilation);
        Assert.False(generator.Executed, "The source generator ran even though no separate consent for it exists (AD-003).");
        Assert.Empty(stripped.AnalyzerReferences.SelectMany(static reference => reference.GetGeneratorsForAllLanguages()));
    }

    [Fact]
    [Trait("Requirement", "GCPC-085")]
    public async Task Strip_ThenGetCompilationAsync_NeverExecutesTheAttachedDiagnosticAnalyzer()
    {
        var analyzer = new CanaryAnalyzer();
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Probe", LanguageNames.CSharp)
            .AddDocument("Probe.cs", "class Probe;")
            .Project
            .WithAnalyzerReferences([new AnalyzerImageReference([analyzer])]);

        // Proof of life: explicitly asking Roslyn to run the canary against the UNSTRIPPED project's own
        // compilation really does execute it -- proving the canary genuinely detects execution.
        var originalCompilation = await project.GetCompilationAsync();
        Assert.NotNull(originalCompilation);
        _ = await originalCompilation!.WithAnalyzers([analyzer]).GetAllDiagnosticsAsync(CancellationToken.None);
        Assert.True(analyzer.Executed, "Proof-of-life failed: the canary analyzer never ran even when explicitly asked to.");

        var stripped = CompilationSanitizer.Strip(project);
        var strippedAnalyzers = stripped.AnalyzerReferences
            .SelectMany(static reference => reference.GetAnalyzersForAllLanguages())
            .ToArray();

        Assert.Empty(strippedAnalyzers);
        var strippedCompilation = await stripped.GetCompilationAsync();
        Assert.NotNull(strippedCompilation);

        // Nothing in the real pipeline ever calls Compilation.WithAnalyzers -- a stripped project
        // carries no analyzer for it to run even if it did, but this proves the call site itself does
        // not exist, so the guard fails loudly (a missing string) if that ever changes.
        var pipelineSource = File.ReadAllText(Path.Combine(
            AnalysisTestPaths.RepoRoot, "src", "Csharp2Md.Analysis", "Semantics", "SemanticAnalysisStage.cs"));
        Assert.DoesNotContain("WithAnalyzers", pipelineSource, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-085")]
    public async Task Strip_RealMsBuildLoadedProject_NeverExecutesTheAttachedCanaryGenerator()
    {
        // Reuses the real MsBuildWorkspaceFactory + CompilationSanitizer call chain
        // (SemanticAnalysisStage.cs:76-78's own "Strip(project).GetCompilationAsync(...)"), attaching
        // the canary to a real, MSBuild-loaded project rather than only an AdhocWorkspace probe.
        var generator = new CanaryGenerator();
        var factory = new MsBuildWorkspaceFactory();
        await using var lease = await factory.Open(AcmeOrdersSolutionPath(), "Debug", "net10.0", CancellationToken.None);
        var project = lease.Solution.Projects
            .First(candidate => candidate.Language == LanguageNames.CSharp
                && candidate.Name.Contains("Acme.Orders", StringComparison.Ordinal))
            .WithAnalyzerReferences([new CanaryGeneratorReference(generator)]);

        var compilation = await CompilationSanitizer.Strip(project).GetCompilationAsync(CancellationToken.None);

        Assert.NotNull(compilation);
        Assert.False(generator.Executed, "The source generator ran on a real, MSBuild-loaded project even without consent (AD-003).");
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

#pragma warning disable RS1042
    private sealed class CanaryGenerator : ISourceGenerator
    {
        public bool Executed { get; private set; }

        public void Reset() => Executed = false;

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context) => Executed = true;
    }
#pragma warning restore RS1042

#pragma warning disable RS1036, RS1038, RS1041, RS2008
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class CanaryAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Descriptor = new(
            "TRUSTGUARD001",
            "Canary",
            "Canary diagnostic",
            "TrustBoundary",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public bool Executed { get; private set; }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Descriptor];

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationAction(_ => Executed = true);
        }
    }
#pragma warning restore RS1036, RS1038, RS1041, RS2008

    private sealed class CanaryGeneratorReference(ISourceGenerator generator) : AnalyzerReference
    {
        public override string FullPath => "canary://generator";

        public override object Id => "canary-generator";

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) =>
            language == LanguageNames.CSharp ? [generator] : [];

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => [generator];
    }
}
