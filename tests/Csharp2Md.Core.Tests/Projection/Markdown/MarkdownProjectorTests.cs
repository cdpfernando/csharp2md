using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Projection.Markdown;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Projection.Markdown;

public sealed class MarkdownProjectorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App.csproj");

    [Fact]
    public void Project_AcceptsFactsOnlyAndPreservesEverySectionPayload()
    {
        const string source = "using System;\nclass C { void Run() { } }\n";
        var fragment = Fragment(source);

        var markdown = MarkdownProjector.Project(fragment);

        var document = Assert.Single(fragment.Facts.OfType<DocumentFact>());
        Assert.All(document.Sections, section => Assert.Contains(section.Source, markdown, StringComparison.Ordinal));
        Assert.Equal(source, string.Concat(document.Sections.OrderBy(static section => section.StartOffset).Select(static section => section.Source)));
    }

    [Fact]
    public void Project_EmptyDocument_ProducesHeadingWithoutCodeSections()
    {
        var markdown = MarkdownProjector.Project(Fragment(string.Empty));

        Assert.Equal("# src/C.cs\n\n", markdown);
        Assert.DoesNotContain("csharp", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_SourceWithoutTrailingNewline_RemainsVerbatimInsideFence()
    {
        const string source = "class C { }";

        var markdown = MarkdownProjector.Project(Fragment(source));

        Assert.Contains("csharp\n}\n```", markdown, StringComparison.Ordinal);
        Assert.Equal(source, string.Concat(Assert.Single(Fragment(source).Facts.OfType<DocumentFact>()).Sections.Select(static section => section.Source)));
    }

    [Fact]
    public void Project_SourceBackticks_UsesLongerFence()
    {
        var markdown = MarkdownProjector.Project(Fragment("class C { string S = \"```\"; }"));

        Assert.Contains("````csharp", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("\n```csharp", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_SymbolAnnotationsRemainOutsideEveryCodePayload()
    {
        const string source = "[Marker] class C { }";
        var fragment = Fragment(source);

        var markdown = MarkdownProjector.Project(fragment);

        Assert.Contains("- `class` â€” syntactic; attributes: Marker", markdown, StringComparison.Ordinal);
        Assert.True(markdown.IndexOf("Factual annotations", StringComparison.Ordinal) < markdown.IndexOf("```csharp", StringComparison.Ordinal));
        Assert.Equal(source, string.Concat(Assert.Single(fragment.Facts.OfType<DocumentFact>()).Sections.Select(static section => section.Source)));
    }

    [Fact]
    public void Project_InvalidPartition_IsRejectedRatherThanSilentlyReordered()
    {
        var fragment = Fragment("class C { }");
        var document = Assert.Single(fragment.Facts.OfType<DocumentFact>());
        var section = document.Sections[0];
        var invalidDocument = document with { Sections = [section with { StartOffset = 1 }] };
        var invalid = new ValidatedFactFragment(
            fragment.Facts.Select(fact => fact is DocumentFact ? invalidDocument : fact).ToImmutableArray(), []);

        Assert.Throws<InvalidOperationException>(() => MarkdownProjector.Project(invalid));
    }

    [Fact]
    public void Project_RepresentativeStructuralKindsHaveDeterministicHeadings()
    {
        const string source = "namespace A;\nenum State { One, Two }\nclass C { int Value; void Run() { } }\n";

        var markdown = MarkdownProjector.Project(Fragment(source));

        Assert.Contains("## Namespace", markdown, StringComparison.Ordinal);
        Assert.Contains("## Enum member", markdown, StringComparison.Ordinal);
        Assert.Contains("## Class", markdown, StringComparison.Ordinal);
        Assert.Contains("## Field", markdown, StringComparison.Ordinal);
        Assert.Contains("## Method", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public Task Project_RepresentativeDocument_MatchesSpecApprovedSnapshot()
    {
        const string source = """
            using System;
            namespace Acme;
            [Marker]
            class Worker
            {
                void Run() { }
            }
            """;

        return Verifier.Verify(MarkdownProjector.Project(Fragment(source)), "md").UseDirectory("snapshots");
    }

    private static ValidatedFactFragment Fragment(string source)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/C.cs", source);
        return new ValidatedFactFragment(
            [extraction.Document, .. extraction.Document.Sections, .. extraction.Symbols],
            []);
    }
}
