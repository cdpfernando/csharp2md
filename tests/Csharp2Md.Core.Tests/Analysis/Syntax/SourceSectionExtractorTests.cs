using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Tests.Analysis.Syntax;

public sealed class SourceSectionExtractorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");

    public static TheoryData<string, string> FidelityCases => new()
    {
        { "empty", string.Empty },
        { "using", "using System;\n" },
        { "block namespace", "namespace A { class C { } }\n" },
        { "file namespace", "namespace A;\nclass C { }\n" },
        { "directives", "#nullable enable\nclass C { }\n" },
        { "regions", "#region R\nclass C { }\n#endregion\n" },
        { "comments", "// header\nclass C { /* body */ } // tail\n" },
        { "top level", "System.Console.WriteLine(1);\n" },
        { "nested types", "class O { class I { int X; } }\n" },
        { "trailing trivia", "class C { }\n\n// trailing\n" },
        { "unicode", "class CafÃ© { string Valor = \"aÃ§Ã£o\"; }\n" },
        { "error source", "class Broken { void M( { #if DEBUG\n" },
    };

    [Theory]
    [MemberData(nameof(FidelityCases))]
    public void Extract_SectionsAreContiguousAndReconstructSource(string name, string source)
    {
        var document = SourceSectionExtractor.Extract(ProjectId, $"src/App/{name.Replace(' ', '-')}.cs", source);

        if (source.Length == 0)
        {
            Assert.Empty(document.Sections);
            return;
        }

        Assert.Equal(0, document.Sections[0].StartOffset);
        Assert.Equal(source.Length, document.Sections[^1].StartOffset + document.Sections[^1].Length);
        Assert.Equal(source, string.Concat(document.Sections.Select(static section => section.Source)));
        for (var index = 1; index < document.Sections.Length; index++)
        {
            Assert.Equal(
                document.Sections[index - 1].StartOffset + document.Sections[index - 1].Length,
                document.Sections[index].StartOffset);
        }
    }

    [Fact]
    public void Extract_StructuralKindsRetainRequiredLanguageShapes()
    {
        const string source = """
            using System;
            namespace Outer;
            #region Types
            // declaration comment
            class Container
            {
                class Nested { }
                void Run() { }
            }
            #endregion
            Console.WriteLine("tail");
            """;

        var document = SourceSectionExtractor.Extract(ProjectId, "src/App/Shapes.cs", source);

        Assert.Contains(document.Sections, static section => section.SectionKind == "preamble");
        Assert.Contains(document.Sections, static section => section.SectionKind == "namespace");
        Assert.Contains(document.Sections, static section => section.SectionKind == "class");
        Assert.Contains(document.Sections, static section => section.SectionKind == "method");
        Assert.Contains("#region Types", string.Concat(document.Sections.Select(static section => section.Source)), StringComparison.Ordinal);
        Assert.Contains("// declaration comment", string.Concat(document.Sections.Select(static section => section.Source)), StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_RepeatedSectionKindsReceiveStableLocalOrdinals()
    {
        const string source = "class C { int A; int B; void A1() { } void B1() { } }";

        var first = SourceSectionExtractor.Extract(ProjectId, "src/App/C.cs", source);
        var relocated = SourceSectionExtractor.Extract(ProjectId, "src/App/C.cs", source);

        Assert.Equal([1, 2], first.Sections.Where(static section => section.SectionKind == "field").Select(static section => section.OccurrenceOrdinal));
        Assert.Equal([1, 2], first.Sections.Where(static section => section.SectionKind == "method").Select(static section => section.OccurrenceOrdinal));
        Assert.Equal(first.Sections.Select(static section => section.Header.Id.Value), relocated.Sections.Select(static section => section.Header.Id.Value));
    }
}
