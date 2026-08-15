using Csharp2Md.Core.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Rendering;

public sealed class SemanticEnricherTests
{
    private const string TypesWithBaseAndInterface = """
        using System;

        namespace Acme.Orders.Internal;

        public abstract class HandlerBase { }

        public sealed class OrderHandler : HandlerBase, IDisposable
        {
            public void Dispose() { }
        }

        """;

    [Fact]
    public void Enrich_HealthySemanticModel_AddsBaseTypeAndImplementedInterfaces()
    {
        var (document, context) = Render(TypesWithBaseAndInterface, withReferences: true);

        var enriched = SemanticEnricher.Enrich(document, context);

        var handler = Assert.Single(enriched.Sections, s => s.Title == "`class OrderHandler`");
        Assert.Contains("**Base type**: `Acme.Orders.Internal.HandlerBase`", handler.Notes);
        Assert.Contains("**Implements**: `System.IDisposable`", handler.Notes);
        Assert.Contains("**Base type**: `Acme.Orders.Internal.HandlerBase`", enriched.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_TypeWithOnlyTheCompilerSuppliedObjectBase_ReportsNoBaseType()
    {
        var (document, context) = Render(TypesWithBaseAndInterface, withReferences: true);

        var enriched = SemanticEnricher.Enrich(document, context);

        var handlerBase = Assert.Single(enriched.Sections, s => s.Title == "`class HandlerBase`");
        Assert.DoesNotContain(handlerBase.Notes, note => note.StartsWith("**Base type**", StringComparison.Ordinal));
    }

    [Fact]
    public void Enrich_NestedNamespace_ResolvesTheDeclaringNamespaceRatherThanTheOutermost()
    {
        const string source = """
            namespace Outer
            {
                namespace Inner
                {
                    public class Deep { }
                }
            }

            """;

        var (document, context) = Render(source, withReferences: true);

        Assert.Equal("Outer", document.Namespace);
        Assert.Equal("Outer.Inner", SemanticEnricher.Enrich(document, context).Namespace);
    }

    [Fact]
    public void Enrich_NullSemanticModel_ReturnsTheDocumentUnchanged()
    {
        var document = RenderTestSources.Render(TypesWithBaseAndInterface, "Handler.cs");
        var context = new RenderContext("Handler.cs", CSharpSyntaxTree.ParseText(TypesWithBaseAndInterface), SemanticModel: null);

        var enriched = SemanticEnricher.Enrich(document, context);

        Assert.Same(document, enriched);
    }

    [Fact]
    public void Enrich_CompilationWithUnresolvedTypes_DoesNotThrowAndInventsNoFacts()
    {
        // No metadata references at all: every base type and interface resolves to an error symbol.
        var (document, context) = Render(TypesWithBaseAndInterface, withReferences: false);

        var enriched = SemanticEnricher.Enrich(document, context);

        Assert.All(
            enriched.Sections,
            section => Assert.DoesNotContain(section.Notes, note => note.StartsWith("**Implements**", StringComparison.Ordinal)));
        Assert.Contains("public sealed class OrderHandler : HandlerBase, IDisposable", enriched.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_SemanticModelBuiltOverADifferentTree_ReturnsTheDocumentUnchanged()
    {
        var (_, context) = Render(TypesWithBaseAndInterface, withReferences: true);
        var otherTree = CSharpSyntaxTree.ParseText(TypesWithBaseAndInterface);
        var document = new MarkdownRenderer().Render(new RenderContext("Handler.cs", otherTree));

        var enriched = SemanticEnricher.Enrich(document, context with { SyntaxTree = otherTree });

        Assert.Same(document, enriched);
    }

    // AD-002: enrichment is additive. It must not disturb the span partition the renderer produced.
    [Fact]
    public void Enrich_LeavesEverySectionSpanAndSourceTextUntouched()
    {
        var (document, context) = Render(TypesWithBaseAndInterface, withReferences: true);

        var enriched = SemanticEnricher.Enrich(document, context);

        Assert.Equal(document.Sections.Select(s => s.Span), enriched.Sections.Select(s => s.Span));
        Assert.Equal(TypesWithBaseAndInterface, string.Concat(enriched.Sections.Select(s => s.Text)));
    }

    private static (RenderedDocument Document, RenderContext Context) Render(string source, bool withReferences)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "Enrichment",
            [tree],
            withReferences ? TestCompilation.PlatformReferences : []);

        var context = new RenderContext("Handler.cs", tree, compilation.GetSemanticModel(tree));
        return (new MarkdownRenderer().Render(context), context);
    }
}
