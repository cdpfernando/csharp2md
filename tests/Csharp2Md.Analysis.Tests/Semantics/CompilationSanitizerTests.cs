using Csharp2Md.Analysis.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit.Sdk;

namespace Csharp2Md.Analysis.Tests.Semantics;

public sealed class CompilationSanitizerTests
{
    [Fact]
    [Trait("Requirement", "ROSE-29")]
    public void Strip_ProjectWithAnalyzerReferences_ReturnsEmptyAnalyzerReferences()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Probe", LanguageNames.CSharp)
            .WithAnalyzerReferences([new NamedAnalyzerReference("Contoso.Analyzers")]);
        Assert.NotEmpty(project.AnalyzerReferences);

        var stripped = CompilationSanitizer.Strip(project);

        Assert.Empty(stripped.AnalyzerReferences);
    }

    [Fact]
    [Trait("Requirement", "ROSE-30")]
    public void Probe_WhenStripIsSkipped_FailsByNamingRemainingAnalyzer()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Probe", LanguageNames.CSharp)
            .WithAnalyzerReferences([new NamedAnalyzerReference("Contoso.Analyzers")]);

        var exception = Assert.ThrowsAny<XunitException>(() => AssertSanitized(project));

        Assert.Contains("Contoso.Analyzers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-30")]
    public void Probe_WhenStripIsSkipped_FailsByNamingRemainingGenerator()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Probe", LanguageNames.CSharp)
            .WithAnalyzerReferences([new NamedAnalyzerReference("Contoso.Generators", new ContosoGenerator())]);

        var exception = Assert.ThrowsAny<XunitException>(() => AssertSanitized(project));

        Assert.Contains(nameof(ContosoGenerator), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "ROSE-30")]
    public async Task Strip_ThenGetCompilationAsync_HasNoAnalyzersOrGenerators()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Probe", LanguageNames.CSharp)
            .AddDocument("Probe.cs", "class Probe;")
            .Project
            .WithAnalyzerReferences([new NamedAnalyzerReference("Contoso.Analyzers", new ContosoGenerator())]);

        var stripped = CompilationSanitizer.Strip(project);
        AssertSanitized(stripped);

        var compilation = await stripped.GetCompilationAsync();
        Assert.NotNull(compilation);
        AssertSanitized(stripped);
    }

    [Fact]
    [Trait("Requirement", "ROSE-30")]
    public async Task Strip_FixtureCompilation_HasNoAnalyzersOrGenerators()
    {
        var factory = new MsBuildWorkspaceFactory();
        await using var lease = await factory.Open(
            AcmeOrdersSolutionPath(),
            "Debug",
            "net10.0",
            CancellationToken.None);

        var sanitizedCompilations = 0;
        foreach (var project in lease.Solution.Projects.Where(candidate => candidate.Language == LanguageNames.CSharp))
        {
            var stripped = CompilationSanitizer.Strip(project);
            AssertSanitized(stripped);
            var compilation = await stripped.GetCompilationAsync();
            if (compilation is null)
            {
                continue;
            }

            AssertSanitized(stripped);
            sanitizedCompilations++;
        }

        Assert.True(
            sanitizedCompilations > 0,
            "Expected at least one loadable C# compilation after Strip.");
    }

    private static void AssertSanitized(Project project)
    {
        var remainingAnalyzers = project.AnalyzerReferences
            .Select(DisplayName)
            .ToArray();
        var remainingGenerators = RemainingGeneratorNames(project.AnalyzerReferences);
        Assert.True(
            remainingAnalyzers.Length == 0 && remainingGenerators.Length == 0,
            "Remaining analyzer(s): " + string.Join(", ", remainingAnalyzers)
            + "; remaining generator(s): " + string.Join(", ", remainingGenerators));
    }

    private static string[] RemainingGeneratorNames(IEnumerable<AnalyzerReference> references) =>
        references
            .SelectMany(reference => reference.GetGeneratorsForAllLanguages().AddRange(reference.GetGenerators(LanguageNames.CSharp)))
            .Select(generator => generator.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string DisplayName(AnalyzerReference reference) =>
        reference.Display ?? reference.FullPath ?? reference.Id.ToString() ?? "<unnamed>";

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

#pragma warning disable RS1042
    private sealed class ContosoGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
        }
    }
#pragma warning restore RS1042

    private sealed class NamedAnalyzerReference : AnalyzerReference
    {
        private readonly ImmutableArray<ISourceGenerator> _generators;

        public NamedAnalyzerReference(string display, params ISourceGenerator[] generators)
        {
            Display = display;
            FullPath = display + ".dll";
            Id = display;
            _generators = [.. generators];
        }

        public override string Display { get; }

        public override string FullPath { get; }

        public override object Id { get; }

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) => _generators;

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => _generators;
    }
}
