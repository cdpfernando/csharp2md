using Csharp2Md.Core.Topic;
using Microsoft.CodeAnalysis.CSharp;

namespace Csharp2Md.Core.Tests.Topic;

/// <summary>
/// Every test parses source with <c>CSharpSyntaxTree.ParseText</c> only. No test constructs a
/// <c>CSharpCompilation</c> or a <c>SemanticModel</c>, matching <see cref="FrontmatterBuilder.Build"/>'s
/// signature, which has no <c>SemanticModel</c> parameter at all (WIKI-13).
/// </summary>
public sealed class FrontmatterBuilderTests
{
    private static TopicOptions Options() => TopicOptions.Create("acme-shop", "system-design", "input-root").Options!;

    [Fact]
    public void Build_ComposesResolvedTitleFileTypeTagsAndOptions()
    {
        const string source = """
            namespace Acme.Orders;

            public sealed class OrderService
            {
                public async System.Threading.Tasks.Task PlaceOrderAsync(IEventBus eventBus)
                {
                    await eventBus.PublishAsync(1);
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);

        var frontmatter = FrontmatterBuilder.Build(
            tree, "Acme.Orders/OrderService.cs", "Acme.Orders", Options(), out var warnings);

        Assert.Equal("OrderService", frontmatter.Title);
        Assert.Equal(SourceKind.CodebaseFile, frontmatter.SourceKind);
        Assert.Equal("Acme.Orders/OrderService.cs", frontmatter.SourcePath);
        Assert.Equal("system-design", frontmatter.Domain);
        Assert.Equal("acme-shop", frontmatter.Topic);
        Assert.Equal(FileType.Service, frontmatter.FileType);
        Assert.Equal(["async-patterns", "event-driven"], frontmatter.Tags);
        Assert.Empty(warnings);
    }

    // WIKI-13: derivation is syntax-only, so a base type that cannot resolve (declared nowhere in
    // the compilation, exactly like Acme.Payments/PaymentsService.cs's real Payments.PaymentsBase)
    // must derive identically to the same source with the base type declared and resolvable.
    [Fact]
    public void Build_UnresolvableBaseType_ProducesSameResultAsResolvableEquivalent()
    {
        const string unresolvable = """
            namespace Acme.Payments;

            public sealed class PaymentsService : Payments.PaymentsBase
            {
            }
            """;
        const string resolvableEquivalent = """
            namespace Payments
            {
                public abstract class PaymentsBase
                {
                }
            }

            namespace Acme.Payments
            {
                public sealed class PaymentsService : Payments.PaymentsBase
                {
                }
            }
            """;

        var unresolvableResult = FrontmatterBuilder.Build(
            CSharpSyntaxTree.ParseText(unresolvable),
            "Acme.Payments/PaymentsService.cs",
            "Acme.Payments",
            Options(),
            out var unresolvableWarnings);
        var resolvableResult = FrontmatterBuilder.Build(
            CSharpSyntaxTree.ParseText(resolvableEquivalent),
            "Acme.Payments/PaymentsService.cs",
            "Acme.Payments",
            Options(),
            out var resolvableWarnings);

        Assert.Equal(resolvableResult.Title, unresolvableResult.Title);
        Assert.Equal(resolvableResult.FileType, unresolvableResult.FileType);
        Assert.Equal(resolvableResult.Tags, unresolvableResult.Tags);
        Assert.Equal(FileType.Service, unresolvableResult.FileType);
        Assert.Empty(unresolvableWarnings);
        Assert.Empty(resolvableWarnings);
    }

    // design.md Risks: a syntax error in a type header can cost the parser the base list while the
    // type's own identifier survives — Foo's base type "Some#Controller" is truncated by the parser
    // to "Some" (the "#Controller" half becomes a preprocessor-directive error), so the Controller
    // rule that a healthy "Foo : SomeController" would have matched silently fails to fire.
    [Fact]
    public void Build_MalformedTypeHeader_ClassifiesAsClassWithWarning()
    {
        const string malformed = "public class Foo : Some#Controller\n{\n}\n";
        var tree = CSharpSyntaxTree.ParseText(malformed);

        var frontmatter = FrontmatterBuilder.Build(
            tree, "Acme.Orders/Foo.cs", "Acme.Orders", Options(), out var warnings);

        Assert.Equal(FileType.Class, frontmatter.FileType);
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, warning => warning.Contains("Acme.Orders/Foo.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_BackslashSeparatedSourcePath_NormalizesToForwardSlashes()
    {
        var tree = CSharpSyntaxTree.ParseText("namespace Acme.Orders;\npublic sealed class OrderService { }");

        var frontmatter = FrontmatterBuilder.Build(
            tree, "Acme.Orders\\OrderService.cs", "Acme.Orders", Options(), out _);

        Assert.Equal("Acme.Orders/OrderService.cs", frontmatter.SourcePath);
        Assert.DoesNotContain('\\', frontmatter.SourcePath);
    }
}
