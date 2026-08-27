using System.Text;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Source;

public sealed class SecretRedactorTests
{
    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Redact_ShortSpan_ReplacesWithSeventeenByteMarker()
    {
        var original = Encoding.UTF8.GetBytes("abXcd").ToImmutableArray();

        var published = SecretRedactor.Redact(original, [new SourceSpanDto(1, 3, 1, 3)]);

        Assert.Equal(17, SecretRedactor.Marker.Length);
        Assert.Equal(Encoding.UTF8.GetBytes("ab" + SecretRedactor.MarkerText + "cd"), published);
    }

    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Redact_LongerSpan_KeepsTheSameMarkerLength()
    {
        var original = Encoding.UTF8.GetBytes("abSUPERSECRETcd").ToImmutableArray();

        var published = SecretRedactor.Redact(original, [new SourceSpanDto(1, 3, 1, 13)]);

        Assert.Equal(17, SecretRedactor.Marker.Length);
        Assert.Equal(Encoding.UTF8.GetBytes("ab" + SecretRedactor.MarkerText + "cd"), published);
    }

    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Redact_MarkerLength_DoesNotDependOnOriginalSpanLength()
    {
        var shortSpan = SecretRedactor.Redact(
            Encoding.UTF8.GetBytes("aXb").ToImmutableArray(),
            [new SourceSpanDto(1, 2, 1, 2)]);
        var longSpan = SecretRedactor.Redact(
            Encoding.UTF8.GetBytes("a" + new string('S', 40) + "b").ToImmutableArray(),
            [new SourceSpanDto(1, 2, 1, 41)]);

        var shortMarker = CountMarkers(shortSpan);
        var longMarker = CountMarkers(longSpan);
        Assert.Equal(17, shortMarker);
        Assert.Equal(17, longMarker);
        Assert.Equal(shortMarker, longMarker);
    }

    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Redact_OverlappingSpans_MergeIntoOneMarker()
    {
        var original = Encoding.UTF8.GetBytes("0123456789").ToImmutableArray();

        var published = SecretRedactor.Redact(
            original,
            [new SourceSpanDto(1, 3, 1, 6), new SourceSpanDto(1, 5, 1, 8)]);

        var text = Encoding.UTF8.GetString(published.AsSpan());
        Assert.Equal("01" + SecretRedactor.MarkerText + "89", text);
        Assert.Equal(1, CountOccurrences(text, SecretRedactor.MarkerText));
    }

    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Redact_OverlappingSpans_DoNotNestMarkers()
    {
        var original = Encoding.UTF8.GetBytes("secret-secret").ToImmutableArray();

        var published = SecretRedactor.Redact(
            original,
            [new SourceSpanDto(1, 1, 1, 8), new SourceSpanDto(1, 5, 1, 13)]);

        var text = Encoding.UTF8.GetString(published.AsSpan());
        Assert.Equal(SecretRedactor.MarkerText, text);
        Assert.Equal(1, CountOccurrences(text, SecretRedactor.MarkerText));
    }

    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Redact_WholeDocumentSpan_YieldsOnlyTheMarker()
    {
        var original = Encoding.UTF8.GetBytes("entire-secret-document").ToImmutableArray();

        var published = SecretRedactor.Redact(original, [new SourceSpanDto(1, 1, 1, original.Length)]);

        Assert.True(SecretRedactor.Marker.AsSpan().SequenceEqual(published.AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Redact_DisjointSpans_EmitsOneMarkerEach()
    {
        var original = Encoding.UTF8.GetBytes("AxxByyC").ToImmutableArray();

        var published = SecretRedactor.Redact(
            original,
            [new SourceSpanDto(1, 2, 1, 3), new SourceSpanDto(1, 5, 1, 6)]);

        Assert.Equal(
            Encoding.UTF8.GetBytes("A" + SecretRedactor.MarkerText + "B" + SecretRedactor.MarkerText + "C"),
            published);
    }

    [Fact]
    [Trait("Requirement", "RP-09")]
    public void Project_SuspectedSecretDiagnostic_RedactsThoseBytesInTheArtifact()
    {
        var secret = "hunter2-value";
        var prefix = "token=";
        var body = prefix + secret;
        var (view, reader, document) = ProjectionPackageFactory.PackageWithSecret(
            "Acme.Orders/Acme.Orders.csproj",
            "Acme.Orders/appsettings.json",
            body,
            new SourceSpanDto(1, prefix.Length + 1, 1, body.Length));

        var fragments = SourceProjector.Project(view, reader);
        var fragment = Assert.Single(
            fragments,
            candidate => !candidate.CanonicalKey.EndsWith(".meta.json", StringComparison.Ordinal));
        var published = Encoding.UTF8.GetString(fragment.ReadPayload().AsSpan());

        Assert.DoesNotContain(secret, published, StringComparison.Ordinal);
        Assert.Equal(prefix + SecretRedactor.MarkerText, published);
        Assert.Equal(document.Reference.Id.Value, view.Document.Documents[0].Identity.Id);
    }

    private static int CountMarkers(ImmutableArray<byte> published)
    {
        var text = Encoding.UTF8.GetString(published.AsSpan());
        Assert.Equal(1, CountOccurrences(text, SecretRedactor.MarkerText));
        return Encoding.UTF8.GetByteCount(SecretRedactor.MarkerText);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var start = 0;
        while ((start = text.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += value.Length;
        }

        return count;
    }
}
