using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Tests.Literals;

public sealed class EvidenceLocatorTests
{
    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void CompareTo_OrdersByDocumentFirst()
    {
        var span = new SourceSpan(1, 1, 1, 5);
        var earlier = new EvidenceLocator(DocumentId.Create("a"), "same.cs", span);
        var later = new EvidenceLocator(DocumentId.Create("b"), "same.cs", span);

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(later.CompareTo(earlier) > 0);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void CompareTo_SameDocument_OrdersByRelativePathSecond()
    {
        var span = new SourceSpan(1, 1, 1, 5);
        var document = DocumentId.Create("doc");
        var earlier = new EvidenceLocator(document, "a/file.cs", span);
        var later = new EvidenceLocator(document, "b/file.cs", span);

        Assert.True(earlier.CompareTo(later) < 0);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void CompareTo_SameDocumentAndPath_OrdersByStartThenEndPosition()
    {
        var document = DocumentId.Create("doc");
        var earlierStart = new EvidenceLocator(document, "file.cs", new SourceSpan(1, 1, 1, 5));
        var laterStart = new EvidenceLocator(document, "file.cs", new SourceSpan(2, 1, 2, 5));
        var sameStartShorterEnd = new EvidenceLocator(document, "file.cs", new SourceSpan(1, 1, 1, 3));
        var sameStartLongerEnd = new EvidenceLocator(document, "file.cs", new SourceSpan(1, 1, 1, 5));

        Assert.True(earlierStart.CompareTo(laterStart) < 0);
        Assert.True(sameStartShorterEnd.CompareTo(sameStartLongerEnd) < 0);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void CompareTo_IsTotal_EqualLocatorsCompareToZero()
    {
        var document = DocumentId.Create("doc");
        var span = new SourceSpan(1, 1, 1, 5);
        var first = new EvidenceLocator(document, "file.cs", span);
        var second = new EvidenceLocator(document, "file.cs", span);

        Assert.Equal(0, first.CompareTo(second));
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void Sorting_SuppliedInTwoDifferentOrders_YieldsTheSameSequence()
    {
        var docA = DocumentId.Create("a");
        var docB = DocumentId.Create("b");
        var locators = new[]
        {
            new EvidenceLocator(docB, "z.cs", new SourceSpan(1, 1, 1, 5)),
            new EvidenceLocator(docA, "a.cs", new SourceSpan(3, 1, 3, 5)),
            new EvidenceLocator(docA, "a.cs", new SourceSpan(1, 1, 1, 5)),
            new EvidenceLocator(docA, "b.cs", new SourceSpan(1, 1, 1, 5)),
        };

        var forward = locators.OrderBy(l => l, Comparer<EvidenceLocator>.Create((x, y) => x.CompareTo(y))).ToArray();
        var reverseInput = locators.Reverse().ToArray();
        var backward = reverseInput.OrderBy(l => l, Comparer<EvidenceLocator>.Create((x, y) => x.CompareTo(y))).ToArray();

        Assert.Equal(forward, backward);
        Assert.Equal(
            new[] { "a.cs:1", "a.cs:3", "b.cs:1", "z.cs:1" },
            forward.Select(l => $"{l.RelativePath}:{l.Span.StartLine}").ToArray());
    }

    [Theory]
    [Trait("Requirement", "TAX-38")]
    [InlineData("/abs/file.cs")]
    [InlineData("C:\\abs\\file.cs")]
    [InlineData("../escape.cs")]
    [InlineData("./file.cs")]
    public void Constructor_AbsoluteOrNonNormalizedRelativePath_IsRejectedNamingTheParameter(string relativePath)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new EvidenceLocator(DocumentId.Create("doc"), relativePath, new SourceSpan(1, 1, 1, 5)));

        Assert.Equal("relativePath", exception.ParamName);
    }
}
