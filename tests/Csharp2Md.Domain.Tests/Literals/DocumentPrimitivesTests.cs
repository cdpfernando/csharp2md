using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Tests.Literals;

public sealed class DocumentPrimitivesTests
{
    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void SourceSpan_EndPrecedingStart_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SourceSpan(5, 1, 4, 1));

        Assert.Equal("endLine", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-38")]
    [InlineData(-1, 1, 2, 1)]
    [InlineData(1, -1, 2, 1)]
    [InlineData(1, 1, -2, 1)]
    [InlineData(1, 1, 2, -1)]
    [InlineData(0, 1, 2, 1)]
    public void SourceSpan_NonPositiveLineOrColumn_IsRejected(int startLine, int startColumn, int endLine, int endColumn)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceSpan(startLine, startColumn, endLine, endColumn));
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void SourceSpan_StartEqualToEnd_IsAccepted()
    {
        var span = new SourceSpan(3, 2, 3, 2);

        Assert.Equal(3, span.StartLine);
        Assert.Equal(2, span.EndColumn);
    }

    [Fact]
    [Trait("Requirement", "TAX-38")]
    public void SourceSpan_HasNoStringMemberAndSoCannotCarryAnAbsolutePath()
    {
        var stringProperties = typeof(SourceSpan).GetProperties().Where(p => p.PropertyType == typeof(string));

        Assert.Empty(stringProperties);
    }

    [Theory]
    [Trait("Requirement", "TAX-81")]
    [InlineData("/abs/path")]
    [InlineData("C:\\abs\\path")]
    [InlineData("\\abs\\path")]
    public void DocumentId_AbsolutePathShapedValue_IsRejectedNamingTheParameter(string absolutePath)
    {
        var exception = Assert.Throws<ArgumentException>(() => DocumentId.Create(absolutePath));

        Assert.Equal("logicalKey", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-81")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("doc  key")]
    [InlineData(" doc-key")]
    public void DocumentId_NonCanonicalValue_IsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => DocumentId.Create(value));
    }

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void DocumentId_CanonicalLogicalKey_IsAcceptedAndIsNotRooted()
    {
        var id = DocumentId.Create("src/Foo/Bar.cs");

        Assert.Equal("src/Foo/Bar.cs", id.Value);
        Assert.False(System.IO.Path.IsPathRooted(id.Value));
    }

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void DocumentHash_EmptyValue_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DocumentHash.Create(""));

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void DocumentHash_TooShort_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DocumentHash.Create(new string('a', 63)));

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void DocumentHash_TooLong_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DocumentHash.Create(new string('a', 65)));

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void DocumentHash_UppercaseHex_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DocumentHash.Create(new string('A', 64)));

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void DocumentHash_NonHexCharacter_IsRejected() =>
        Assert.Throws<ArgumentException>(() => DocumentHash.Create(new string('g', 64)));

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void DocumentHash_ValidLowercaseHexDigest_IsAcceptedAndIsNotAnAbsolutePath()
    {
        var digest = new string('a', 64);

        var hash = DocumentHash.Create(digest);

        Assert.Equal(digest, hash.Value);
        Assert.Equal(64, hash.Value.Length);
        Assert.False(System.IO.Path.IsPathRooted(hash.Value));
    }
}
