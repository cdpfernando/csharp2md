using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Tests.Literals;

public sealed class DeclarationLocatorTests
{
    private static DocumentId Document => DocumentId.Create("src/Acme.Payments/Invoice.cs");

    private static SourceSpan Span => new(10, 1, 24, 2);

    private static DocumentHash Hash => DocumentHash.Create(new string('a', 64));

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void Constructor_ValidComponents_ExposesDocumentSpanAndHash()
    {
        var locator = new DeclarationLocator(Document, "src/Acme.Payments/Invoice.cs", Span, Hash);

        Assert.Equal(Document, locator.Document);
        Assert.Equal("src/Acme.Payments/Invoice.cs", locator.RelativePath);
        Assert.Equal(Span, locator.Span);
        Assert.Equal(Hash, locator.Hash);
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void Constructor_UninitializedDocument_IsRejectedNamingDocument()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new DeclarationLocator(default, "src/Acme.Payments/Invoice.cs", Span, Hash));

        Assert.Equal("document", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "RP-13")]
    public void Constructor_UninitializedHash_IsRejectedNamingHash()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new DeclarationLocator(Document, "src/Acme.Payments/Invoice.cs", Span, default));

        Assert.Equal("hash", exception.ParamName);
    }
}
