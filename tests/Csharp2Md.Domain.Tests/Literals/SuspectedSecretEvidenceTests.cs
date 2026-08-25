using System.Reflection;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Domain.Tests.Literals;

public sealed class SuspectedSecretEvidenceTests
{
    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void RedactedExcerpt_ContainingAMaskMarker_IsAccepted()
    {
        var excerpt = RedactedExcerpt.Create("ConnectionString=***");

        Assert.Equal("ConnectionString=***", excerpt.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void RedactedExcerpt_ContainingARedactedMarker_IsAccepted()
    {
        var excerpt = RedactedExcerpt.Create("apiKey: [REDACTED]");

        Assert.Equal("apiKey: [REDACTED]", excerpt.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-82")]
    public void RedactedExcerpt_WithNoRedactionMarker_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentException>(() => RedactedExcerpt.Create("ConnectionString=Server=x;Password=hunter2"));

        Assert.Equal("excerpt", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-82")]
    public void RedactedExcerpt_EmptyOrWhitespace_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => RedactedExcerpt.Create(""));
        Assert.Throws<ArgumentException>(() => RedactedExcerpt.Create("   "));
    }

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void Create_CarriesExactlyDocumentSpanHashAndExcerpt()
    {
        var document = DocumentId.Create("doc");
        var span = new SourceSpan(1, 1, 1, 5);
        var hash = DocumentHash.Create(new string('a', 64));
        var excerpt = RedactedExcerpt.Create("secret=***");

        var evidence = SuspectedSecretEvidence.Create(document, span, hash, excerpt);

        Assert.Equal(document, evidence.Document);
        Assert.Equal(span, evidence.Span);
        Assert.Equal(hash, evidence.Hash);
        Assert.Equal(excerpt, evidence.Excerpt);
    }

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void Type_ExposesExactlyFourPublicProperties()
    {
        var properties = typeof(SuspectedSecretEvidence)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType == typeof(SuspectedSecretEvidence))
            .ToArray();

        Assert.Equal(4, properties.Length);
        Assert.Contains(properties, p => p.PropertyType == typeof(DocumentId));
        Assert.Contains(properties, p => p.PropertyType == typeof(SourceSpan));
        Assert.Contains(properties, p => p.PropertyType == typeof(DocumentHash));
        Assert.Contains(properties, p => p.PropertyType == typeof(RedactedExcerpt));
    }

    [Fact]
    [Trait("Requirement", "TAX-82")]
    public void Type_NoMemberCarriesAnOriginalLiteralOrAPerSecretHash_ByNameAndByType()
    {
        string[] forbiddenNameFragments = ["Literal", "Secret", "Original", "Token", "PlainText"];
        var properties = typeof(SuspectedSecretEvidence).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            Assert.DoesNotContain(forbiddenNameFragments, fragment => property.Name.Contains(fragment, StringComparison.Ordinal));
            Assert.NotEqual(typeof(string), property.PropertyType);
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-81")]
    public void Hash_IsTheWholeDocumentHashSuppliedByTheCaller_NotDerivedFromTheSpan()
    {
        var document = DocumentId.Create("doc");
        var hash = DocumentHash.Create(new string('b', 64));
        var excerpt = RedactedExcerpt.Create("x=***");

        var narrowSpan = SuspectedSecretEvidence.Create(document, new SourceSpan(1, 1, 1, 2), hash, excerpt);
        var wideSpan = SuspectedSecretEvidence.Create(document, new SourceSpan(1, 1, 50, 2), hash, excerpt);

        Assert.Equal(hash, narrowSpan.Hash);
        Assert.Equal(hash, wideSpan.Hash);
        Assert.Equal(narrowSpan.Hash, wideSpan.Hash);
    }
}
