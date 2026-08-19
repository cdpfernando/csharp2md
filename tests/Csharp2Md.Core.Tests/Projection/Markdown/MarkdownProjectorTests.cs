using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
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
    public void Project_AnalysisSectionRemainsOutsideEveryCodePayload()
    {
        const string source = "[Marker] class C { }";
        var fragment = Fragment(source);

        var markdown = MarkdownProjector.Project(fragment);

        Assert.Contains("resolution: syntactic", markdown, StringComparison.Ordinal);
        Assert.Contains("symbols:\n  syntactic: 1", markdown, StringComparison.Ordinal);
        Assert.Contains("relations: {}", markdown, StringComparison.Ordinal);
        Assert.Contains("diagnostics: {}", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("- `class`", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes:", markdown, StringComparison.Ordinal);
        Assert.True(markdown.IndexOf("## Analysis", StringComparison.Ordinal) < markdown.IndexOf("```csharp", StringComparison.Ordinal));
        Assert.Equal(source, string.Concat(Assert.Single(fragment.Facts.OfType<DocumentFact>()).Sections.Select(static section => section.Source)));
    }

    [Fact]
    public void Project_AnalysisBlock_ZeroSymbolsRenderAsEmptyMapWhenRelationsPresent()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/C.cs", string.Empty);
        var documentId = extraction.Document.DocumentId;
        var fragment = new ValidatedFactFragment(
            [extraction.Document, .. extraction.Document.Sections, MakeRelation(documentId, "http-request", FactResolution.Exact)],
            []);

        var markdown = MarkdownProjector.Project(fragment);

        Assert.Contains("symbols: {}", markdown, StringComparison.Ordinal);
        Assert.Contains("relations:\n  exact: 1", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_AnalysisBlock_ResolutionLineReflectsDocumentHeaderResolutionNotAHardcodedValue()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/C.cs", "class C { }");
        var exactHeader = FactHeader.Create(extraction.Document.Header.Id, FactKind.Document, FactResolution.Exact);
        var document = extraction.Document with { Header = exactHeader };
        var fragment = new ValidatedFactFragment(
            [document, .. document.Sections, .. extraction.Symbols],
            []);

        var markdown = MarkdownProjector.Project(fragment);

        Assert.Contains("resolution: exact", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("resolution: syntactic", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_AnalysisBlock_IsDeterministicAcrossRepeatedProjection()
    {
        var fragment = Fragment("class C { void Run() { } }");

        var first = MarkdownProjector.Project(fragment);
        var second = MarkdownProjector.Project(fragment);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Project_AnalysisBlock_AggregatesResolutionKindsAndDiagnosticsByCode()
    {
        const string source = "class C { void Run() { } }";
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/C.cs", source);
        var documentId = extraction.Document.DocumentId;
        var extraSymbols = new[]
        {
            MakeSymbol(documentId, "class", FactResolution.Exact, "extra-exact"),
            MakeSymbol(documentId, "class", FactResolution.Unresolved, "extra-unresolved"),
        };
        var relations = new[]
        {
            MakeRelation(documentId, "http-request-1", FactResolution.Exact),
            MakeRelation(documentId, "http-request-2", FactResolution.Unresolved),
        };
        var diagnostics = new[]
        {
            MakeDiagnostic(documentId, DiagnosticSeverity.Warning, "first occurrence"),
            MakeDiagnostic(documentId, DiagnosticSeverity.Error, "second occurrence"),
        };
        var fragment = new ValidatedFactFragment(
            [
                extraction.Document, .. extraction.Document.Sections, .. extraction.Symbols,
                .. extraSymbols, .. relations,
            ],
            [.. diagnostics]);

        var markdown = MarkdownProjector.Project(fragment);

        Assert.Contains(
            "```yaml\nresolution: syntactic\nsymbols:\n  exact: 1\n  syntactic: 2\n  unresolved: 1\n"
                + "relations:\n  exact: 1\n  unresolved: 1\ndiagnostics:\n  C2M-BIND-002: 2\n```",
            markdown,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Project_AnalysisBlock_DiagnosticsAreOrderedByCodeOrdinalNotEncounterOrder()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/C.cs", "class C { }");
        var documentId = extraction.Document.DocumentId;
        var diagnostics = new[]
        {
            MakeDiagnostic(documentId, DiagnosticSeverity.Warning, "encountered first", "C2M-BIND-900"),
            MakeDiagnostic(documentId, DiagnosticSeverity.Warning, "encountered second", "C2M-BIND-001"),
        };
        var fragment = new ValidatedFactFragment(
            [extraction.Document, .. extraction.Document.Sections, .. extraction.Symbols],
            [.. diagnostics]);

        var markdown = MarkdownProjector.Project(fragment);

        Assert.Contains("diagnostics:\n  C2M-BIND-001: 1\n  C2M-BIND-900: 1\n```", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_AnalysisBlock_NotApplicableSymbolIsCountedNotDropped()
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/C.cs", "class C { }");
        var documentId = extraction.Document.DocumentId;
        var notApplicable = MakeSymbol(documentId, "class", FactResolution.NotApplicable, "not-applicable");
        var fragment = new ValidatedFactFragment(
            [extraction.Document, .. extraction.Document.Sections, .. extraction.Symbols, notApplicable],
            []);

        var markdown = MarkdownProjector.Project(fragment);

        Assert.Contains("symbols:\n  syntactic: 1\n  notapplicable: 1\n", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_AnalysisBlock_FenceStaysPlainRegardlessOfSourceBacktickRun()
    {
        const string source = "class C { string S = \"```\"; }";

        var markdown = MarkdownProjector.Project(Fragment(source));

        Assert.Contains("## Analysis\n\n```yaml\n", markdown, StringComparison.Ordinal);
        Assert.Contains("````csharp", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_NoSymbolsRelationsOrDiagnostics_OmitsAnalysisSection()
    {
        var markdown = MarkdownProjector.Project(Fragment(string.Empty));

        Assert.DoesNotContain("## Analysis", markdown, StringComparison.Ordinal);
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

    private static SymbolFact MakeSymbol(DocumentFactId documentId, string kind, FactResolution resolution, string discriminator)
    {
        var symbolId = SymbolFactId.CreateSyntactic(ProjectId, "src/C.cs", kind, discriminator);
        return new SymbolFact(
            FactHeader.Create(symbolId.ToFactId(), FactKind.Symbol, resolution),
            symbolId,
            documentId,
            kind,
            ContainsErrorSymbol: false,
            [],
            [],
            []);
    }

    private static RelationFact MakeRelation(DocumentFactId documentId, string claim, FactResolution resolution)
    {
        var relationId = RelationFactId.Create(documentId.ToFactId(), "http", claim, 1);
        return new RelationFact(
            FactHeader.Create(relationId.ToFactId(), FactKind.Relation, resolution),
            relationId,
            documentId.ToFactId(),
            TargetId: null,
            RelationPartition.Http,
            "http-request",
            resolution == FactResolution.Unresolved ? "no target proved" : null);
    }

    private static AnalysisDiagnostic MakeDiagnostic(
        DocumentFactId documentId, DiagnosticSeverity severity, string message, string code = "C2M-BIND-002") =>
        AnalysisDiagnostic.Create(code, severity, DiagnosticStage.Detector, documentId.ToFactId(), message);
}
