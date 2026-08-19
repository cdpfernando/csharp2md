using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Analysis.Semantics.Roslyn;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis;

[Trait("Category", "Integration")]
public sealed class LanguageMatrixTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/Matrix/Matrix.csproj");
    private static readonly TargetFactId TargetId = TargetFactId.Create(ProjectId, "net10.0");

    public static TheoryData<string, string, string[], string> LanguageShapes => new()
    {
        { "overloads", "namespace Acme; public sealed class Api { public void Run(int value) { } public void Run(string value) { } }", ["namespace", "class", "method", "method"], "System.Int32" },
        { "generic type", "namespace Acme; public sealed class Box<T> { }", ["namespace", "class"], "Box%601" },
        { "generic method", "namespace Acme; public sealed class Api { public T Echo<T>(T value) => value; }", ["namespace", "class", "method"], "Echo" },
        { "record", "namespace Acme; public sealed record Payment(string Id);", ["namespace", "record"], "T%3AAcme.Payment" },
        { "record struct", "namespace Acme; public readonly record struct Money(decimal Amount);", ["namespace", "record-struct"], "T%3AAcme.Money" },
        { "interface", "namespace Acme; public interface IProbe { void Run(); }", ["namespace", "interface", "method"], "T%3AAcme.IProbe" },
        { "implementation", "namespace Acme; public interface IProbe { void Run(); } public sealed class Probe : IProbe { public void Run() { } }", ["namespace", "interface", "method", "class", "method"], "M%3AAcme.Probe.Run" },
        { "override", "namespace Acme; public class Base { public virtual void Run() { } } public sealed class Derived : Base { public override void Run() { } }", ["namespace", "class", "method", "class", "method"], "M%3AAcme.Derived.Run" },
        { "conditional", "#if ENABLED\nnamespace Acme; public sealed class Enabled { }\n#else\nnamespace Acme; public sealed class Disabled { }\n#endif", ["namespace", "class"], "Disabled" },
        { "nested generic interface", "namespace Acme; public interface IRepository<T> { T Get(); } public sealed class Repository : IRepository<string> { public string Get() => string.Empty; }", ["namespace", "interface", "method", "class", "method"], "M%3AAcme.Repository.Get" },
    };

    [Theory]
    [MemberData(nameof(LanguageShapes))]
    public void LanguageShapes_ProduceSpecifiedSyntacticAndExactSemanticFacts(
        string name,
        string source,
        string[] expectedKinds,
        string expectedResolvedIdentity)
    {
        var syntax = SyntaxFactExtractor.Extract(ProjectId, $"src/Matrix/{name}.cs", source);
        var enriched = Enrich(name, source);

        Assert.Equal(expectedKinds.Order(StringComparer.Ordinal), syntax.Symbols.Select(static symbol => symbol.SymbolKind).Order(StringComparer.Ordinal));
        Assert.All(syntax.Symbols, static symbol => Assert.Equal(FactResolution.Syntactic, symbol.Header.Resolution));
        Assert.Equal(expectedKinds.Order(StringComparer.Ordinal), enriched.Symbols.Select(static symbol => symbol.SymbolKind).Order(StringComparer.Ordinal));
        Assert.All(enriched.Symbols, static symbol => Assert.Equal(FactResolution.Exact, symbol.Header.Resolution));
        Assert.Contains(enriched.Symbols, symbol => symbol.SymbolId.Value.Contains(expectedResolvedIdentity, StringComparison.Ordinal));
    }

    [Fact]
    public void ErrorSymbols_RetainSyntacticIdentityAndNeverBecomeExact()
    {
        const string source = "namespace Acme; public sealed class Probe { public MissingType Value = null!; }";

        var syntax = SyntaxFactExtractor.Extract(ProjectId, "src/Matrix/Error.cs", source);
        var enriched = Enrich("Error", source);
        var syntacticField = syntax.Symbols.Single(static symbol => symbol.SymbolKind == "field");
        var unresolvedField = enriched.Symbols.Single(static symbol => symbol.SymbolKind == "field");

        Assert.False(syntacticField.ContainsErrorSymbol);
        Assert.Equal(syntacticField.SymbolId, unresolvedField.SymbolId);
        Assert.Equal(FactResolution.Unresolved, unresolvedField.Header.Resolution);
        Assert.True(unresolvedField.ContainsErrorSymbol);
        Assert.DoesNotContain(enriched.Symbols, symbol => symbol.SymbolKind == "field" && symbol.Header.Resolution == FactResolution.Exact);
    }

    [Fact]
    public void StableSymbols_KeepTheirIdentityWhenPrecedingSourceMovesTheirLocation()
    {
        const string declaration = "namespace Acme { public sealed class Stable { public void Run() { } } }";
        const string precedingSource = "namespace Other { public sealed class Earlier { } }\n";

        var originalSyntax = SyntaxFactExtractor.Extract(ProjectId, "src/Matrix/Stable.cs", declaration);
        var shiftedSyntax = SyntaxFactExtractor.Extract(ProjectId, "src/Matrix/Stable.cs", precedingSource + declaration);
        var originalStable = originalSyntax.Symbols.Single(static symbol => symbol.SymbolId.Value.Contains("class%3AStable", StringComparison.Ordinal));
        var shiftedStable = shiftedSyntax.Symbols.Single(static symbol => symbol.SymbolId.Value.Contains("class%3AStable", StringComparison.Ordinal));
        var originalSections = SourceSectionExtractor.Extract(ProjectId, "src/Matrix/Stable.cs", declaration);
        var shiftedSections = SourceSectionExtractor.Extract(ProjectId, "src/Matrix/Stable.cs", precedingSource + declaration);
        var originalStableSection = originalSections.Sections.Single(static section => section.SectionKind == "class" && section.Source.Contains("class Stable", StringComparison.Ordinal));
        var shiftedStableSection = shiftedSections.Sections.Single(static section => section.SectionKind == "class" && section.Source.Contains("class Stable", StringComparison.Ordinal));

        Assert.Equal(originalStable.SymbolId, shiftedStable.SymbolId);
        Assert.Equal(originalStableSection.Source, shiftedStableSection.Source);
        Assert.True(shiftedStableSection.StartOffset > originalStableSection.StartOffset);
        Assert.Equal(declaration, string.Concat(originalSections.Sections.Select(static section => section.Source)));
        Assert.Equal(precedingSource + declaration, string.Concat(shiftedSections.Sections.Select(static section => section.Source)));
    }

    private static SymbolFactEnrichmentResult Enrich(string name, string source)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, $"src/Matrix/{name}.cs", source);
        var target = new EvaluatedTarget(
            TargetId,
            "net10.0",
            Succeeded: true,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TargetFramework"] = "net10.0",
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
        var compilation = new SemanticCompilationAdapter().CreateCompilation(
            new SemanticCompilationRequest(
                target,
                "Matrix",
                [new SemanticSourceDocument(extraction.Document.DocumentId, $"src/Matrix/{name}.cs", source)]),
            CancellationToken.None);

        return new SymbolFactEnricher().Enrich(
            extraction.Document,
            extraction.Symbols,
            Assert.Single(compilation.Documents),
            TargetId,
            CancellationToken.None);
    }
}
