using Csharp2Md.Core.Rendering;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Rendering;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void Render_EmitsFileHeadingWithNamespaceAndIndexBacklink()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace, "Orders/Sub/OrderService.cs");

        var markdown = document.ToMarkdown();

        Assert.StartsWith("# Orders/Sub/OrderService.cs\n", markdown, StringComparison.Ordinal);
        Assert.Equal("Acme.Orders", document.Namespace);
        Assert.Equal("../../index.md", document.IndexLink);
        Assert.Contains("Namespace: `Acme.Orders` | [Index](../../index.md)", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_DocumentAtServiceRoot_LinksToTheSiblingIndex()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace, "OrderService.cs");

        Assert.Equal("./index.md", document.IndexLink);
    }

    [Fact]
    public void Render_FileWithoutNamespaceDeclaration_ReportsGlobalNamespace()
    {
        var document = RenderTestSources.Render(RenderTestSources.TopLevelStatements, "Program.cs");

        Assert.Equal("(global)", document.Namespace);
    }

    [Fact]
    public void Render_PreambleSection_CarriesUsingsAndFileHeaderComment()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace);

        var preamble = Assert.Single(document.Sections, s => s.Title == "Preamble");

        Assert.Contains("using System;", preamble.Text, StringComparison.Ordinal);
        Assert.Contains("using System.Threading.Tasks;", preamble.Text, StringComparison.Ordinal);
        Assert.Contains("// File header comment.", preamble.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EmitsOneSectionPerTypeAndOneSubsectionPerMember()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace);

        var type = Assert.Single(document.Sections, s => s.Title == "`class OrderService`");
        var method = Assert.Single(document.Sections, s => s.Title == "Method `PlaceOrderAsync`");
        var field = Assert.Single(document.Sections, s => s.Title == "Field `_retries`");

        // Namespace at depth 0 => level 2, type nested inside it => 3, its members => 4.
        Assert.Equal(2, Assert.Single(document.Sections, s => s.Title == "Namespace `Acme.Orders`").Level);
        Assert.Equal(3, type.Level);
        Assert.Equal(4, method.Level);
        Assert.Equal(4, field.Level);
    }

    // P1-12: full body text, never summarized or truncated.
    [Fact]
    public void Render_MethodBody_IsEmittedVerbatimAndComplete()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace);

        var method = Assert.Single(document.Sections, s => s.Title == "Method `PlaceOrderAsync`");

        Assert.Contains("public async Task PlaceOrderAsync(int id)", method.Text, StringComparison.Ordinal);
        Assert.Contains("for (var i = 0; i < _retries; i++)", method.Text, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(id);", method.Text, StringComparison.Ordinal);
        Assert.Contains("await Task.Delay(1);", method.Text, StringComparison.Ordinal);
        Assert.Contains(method.Text, document.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_XmlDocComment_IsRenderedAsProseAboveTheCodeFence()
    {
        var document = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace);

        var method = Assert.Single(document.Sections, s => s.Title == "Method `PlaceOrderAsync`");

        Assert.Equal(
            new[] { "**summary**: Places an order.", "**param** `id`: The order id." },
            method.Notes);
        Assert.Contains("**summary**: Places an order.", document.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_BodylessRecordDeclaration_BecomesASingleSection()
    {
        var document = RenderTestSources.Render(RenderTestSources.RecordWithoutBody, "Contracts.cs");

        var record = Assert.Single(document.Sections, s => s.Title == "`record OrderPlaced`");

        Assert.Contains("public sealed record OrderPlaced(int Id, string Customer);", record.Text, StringComparison.Ordinal);
        Assert.Single(document.Sections, s => s.Title == "`record struct Money`");
    }

    [Fact]
    public void Render_EnumMembers_EachBecomeTheirOwnSubsection()
    {
        var document = RenderTestSources.Render(RenderTestSources.NestedTypesAndEnum, "Status.cs");

        Assert.Single(document.Sections, s => s.Title == "`enum Status`");
        Assert.Single(document.Sections, s => s.Title == "Enum member `Pending`");
        Assert.Single(document.Sections, s => s.Title == "Enum member `Shipped`");
        Assert.Single(document.Sections, s => s.Title == "Enum member `Cancelled`");
    }

    // AD-002: the renderer is syntax-driven, so a project that failed to restore still renders.
    [Fact]
    public void Render_WithNullSemanticModelAndUnresolvableTypes_StillRendersFullSource()
    {
        const string source = """
            using Totally.Missing.Package;

            namespace Acme.Orders;

            public class Consumer
            {
                public MissingType Handle(OtherMissingType input) => input.Convert();
            }

            """;

        var document = new MarkdownRenderer().Render(
            new RenderContext("Consumer.cs", CSharpSyntaxTree.ParseText(source), SemanticModel: null));

        Assert.Equal(source, string.Concat(document.Sections.Select(s => s.Text)));
        Assert.Contains(
            "public MissingType Handle(OtherMissingType input) => input.Convert();",
            Assert.Single(document.Sections, s => s.Title == "Method `Handle`").Text,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Delegate `WidgetChanged`")]
    [InlineData("`interface IResettable`")]
    [InlineData("Event `Field`")]
    [InlineData("Event `Property`")]
    [InlineData("Field `_value, _other`")]
    [InlineData("Constructor `Widget`")]
    [InlineData("Finalizer `Widget`")]
    [InlineData("Property `Value`")]
    [InlineData("Indexer `this[]`")]
    [InlineData("Operator `+`")]
    [InlineData("Conversion operator")]
    [InlineData("Method `Reset`")]
    [InlineData("`struct Nested`")]
    public void Render_TitlesEveryMemberKindItEmits(string expectedTitle)
    {
        var document = RenderTestSources.Render(RenderTestSources.AllMemberKinds, "Widget.cs");

        Assert.Contains(expectedTitle, document.Sections.Select(s => s.Title));
    }

    [Fact]
    public void Render_DocCommentWithACrefReference_RendersTheReferencedNameInProse()
    {
        var document = RenderTestSources.Render(RenderTestSources.AllMemberKinds, "Widget.cs");

        var property = Assert.Single(document.Sections, s => s.Title == "Property `Value`");

        Assert.Equal(["**summary**: Gets the value held by this Widget."], property.Notes);
    }

    [Fact]
    public void Render_DocCommentWithParamrefReferences_RendersTheReferencedParameterNames()
    {
        var document = RenderTestSources.Render(RenderTestSources.AllMemberKinds, "Widget.cs");

        var op = Assert.Single(document.Sections, s => s.Title == "Operator `+`");

        Assert.Equal(["**summary**: Adds left to right."], op.Notes);
    }

    [Fact]
    public Task Render_EmittedMarkdownShape_MatchesApprovedSnapshot()
    {
        var markdown = RenderTestSources.Render(RenderTestSources.UsingsAndBlockNamespace).ToMarkdown();

        return Verifier.Verify(markdown).UseDirectory("snapshots");
    }
}
