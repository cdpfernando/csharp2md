using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Tests.Analysis.Syntax;

public sealed class SyntaxFactExtractorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");

    public static TheoryData<string, string, string[]> DeclarationCases => new()
    {
        { "namespace", "namespace A; class C { }", ["namespace", "class"] },
        { "overloads", "class C { void M(int value) { } void M(string value) { } }", ["class", "method", "method"] },
        { "generic type", "class Box<T> where T : class { T Value; }", ["class", "field"] },
        { "generic method", "class C { T Echo<T>(T value) => value; }", ["class", "method"] },
        { "record", "record Person(string Name);", ["record"] },
        { "record struct", "readonly record struct Money(decimal Amount);", ["record-struct"] },
        { "interface", "interface IRun { void Run(); }", ["interface", "method"] },
        { "override", "class B { public virtual void M() { } } class D : B { public override void M() { } }", ["class", "method", "class", "method"] },
        { "members", "class C { int F; string P { get; } event Action E; C() { } }", ["class", "field", "property", "event", "constructor"] },
        { "operator", "struct S { public static S operator +(S a, S b) => a; }", ["struct", "operator"] },
        { "delegate", "delegate void Work<T>(T value);", ["delegate"] },
        { "enum", "enum State { One, Two }", ["enum", "enum-member", "enum-member"] },
        { "conditional", "#if DEBUG\nclass DebugOnly { }\n#else\nclass ReleaseOnly { }\n#endif", ["class"] },
        { "error", "class Broken<T { void M( }", ["class", "method"] },
    };

    [Theory]
    [MemberData(nameof(DeclarationCases))]
    public void Extract_EmitsSpecDefinedSyntacticDeclarationKinds(string name, string source, string[] expectedKinds)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, $"src/App/{name.Replace(' ', '-')}.cs", source);

        Assert.Equal(expectedKinds.Order(StringComparer.Ordinal), extraction.Symbols.Select(static symbol => symbol.SymbolKind).Order(StringComparer.Ordinal));
        Assert.All(extraction.Symbols, static symbol => Assert.Equal("Syntactic", symbol.Header.Resolution.ToString()));
        Assert.Equal(extraction.Symbols.Select(static symbol => symbol.SymbolId), extraction.Document.SymbolIds);
    }

    [Fact]
    public void Extract_OverloadsHaveDistinctStableIdsThatIgnoreBodies()
    {
        const string original = "class C { void M(int first) { Console.WriteLine(1); } void M(string text) { } }";
        const string changedBodies = "class C { void M(int first) { throw null!; } void M(string text) { Console.WriteLine(text); } }";

        var originalIds = Methods(original);
        var changedIds = Methods(changedBodies);

        Assert.Equal(2, originalIds.Length);
        Assert.Equal(2, originalIds.Distinct().Count());
        Assert.Equal(originalIds, changedIds);
    }

    [Fact]
    public void Extract_IdsSurviveAbsoluteRootRelocationAndUnrelatedPrecedingDeclaration()
    {
        const string declaration = "namespace A; class Stable { void Run(int value) { } }";
        const string withPreceding = "namespace A; class Earlier { } class Stable { void Run(int value) { } }";

        var first = SyntaxFactExtractor.Extract(ProjectId, "src/App/Stable.cs", declaration);
        var second = SyntaxFactExtractor.Extract(ProjectId, "src/App/Stable.cs", withPreceding);

        var firstStable = first.Symbols.Where(static symbol => symbol.SymbolId.Value.Contains("class%3AStable", StringComparison.Ordinal)).Select(static symbol => symbol.SymbolId.Value);
        var secondStable = second.Symbols.Where(static symbol => symbol.SymbolId.Value.Contains("class%3AStable", StringComparison.Ordinal)).Select(static symbol => symbol.SymbolId.Value);
        Assert.Equal(firstStable, secondStable);
        Assert.DoesNotContain(first.Symbols, static symbol => symbol.SymbolId.Value.Contains("span", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_BaseTypesAttributesReferencesAndXmlProseAreRetainedAsSyntacticEvidence()
    {
        const string source = """
            namespace A;
            /// <summary>Runs work.</summary>
            [Marker]
            class Worker : Base, IDisposable
            {
                string Run(int count) => count.ToString();
            }
            """;

        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Worker.cs", source);
        var worker = Assert.Single(extraction.Symbols, static symbol => symbol.SymbolKind == "class");
        var method = Assert.Single(extraction.Symbols, static symbol => symbol.SymbolKind == "method");

        Assert.Equal(["Marker"], worker.Attributes.ToArray());
        Assert.Equal(["Base", "IDisposable"], extraction.RelationCandidates.Select(static candidate => candidate.ObservedTarget));
        Assert.Equal(["int", "string"], method.RelevantTypeReferences.ToArray());
        Assert.Contains("Runs work.", Assert.Single(extraction.XmlProse[worker.SymbolId]), StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ErrorBearingDeclarationRemainsSyntacticAndMarked()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/App/Broken.cs", "class Broken<T { void Run( }");

        Assert.Contains(extraction.Symbols, static symbol => symbol.ContainsErrorSymbol);
        Assert.All(extraction.Symbols, static symbol => Assert.Equal(Csharp2Md.Core.Facts.Model.FactResolution.Syntactic, symbol.Header.Resolution));
    }

    private static string[] Methods(string source) =>
        SyntaxFactExtractor.Extract(ProjectId, "src/App/C.cs", source).Symbols
            .Where(static symbol => symbol.SymbolKind == "method")
            .Select(static symbol => symbol.SymbolId.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
