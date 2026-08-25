using Csharp2Md.Analysis.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

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

    private sealed class NamedAnalyzerReference : AnalyzerReference
    {
        public NamedAnalyzerReference(string display)
        {
            Display = display;
            FullPath = display + ".dll";
            Id = display;
        }

        public override string Display { get; }

        public override string FullPath { get; }

        public override object Id { get; }

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];
    }
}
