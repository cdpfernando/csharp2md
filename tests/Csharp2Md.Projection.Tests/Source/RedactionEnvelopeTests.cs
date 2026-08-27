using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Source;

public sealed class RedactionEnvelopeTests
{
    [Fact]
    [Trait("Requirement", "RP-10")]
    public void Project_RedactedDocument_EmitsEnvelopeWithBothHashes()
    {
        var body = "token=hunter2-value";
        var (view, reader, document) = ProjectionPackageFactory.PackageWithSecret(
            "Acme.Orders/Acme.Orders.csproj",
            "Acme.Orders/appsettings.json",
            body,
            new SourceSpanDto(1, 7, 1, body.Length));

        var fragments = SourceProjector.Project(view, reader);
        var source = Assert.Single(
            fragments,
            fragment => !fragment.CanonicalKey.EndsWith(".meta.json", StringComparison.Ordinal));
        var envelopeFragment = Assert.Single(
            fragments,
            fragment => fragment.CanonicalKey.EndsWith(".meta.json", StringComparison.Ordinal));
        var envelope = CanonicalJson.Read<RedactionEnvelopeDto>(envelopeFragment.ReadPayload().AsSpan());
        var published = source.ReadPayload();

        Assert.True(envelope.Redacted);
        Assert.Equal(document.Reference.Id.Value, envelope.Document);
        Assert.Equal(source.CanonicalKey, envelope.Artifact);
        Assert.Equal(envelopeFragment.CanonicalKey, source.CanonicalKey + ".meta.json");
        Assert.Equal(Sha256(Encoding.UTF8.GetBytes(body)), envelope.OriginalSha256);
        Assert.Equal(Sha256(published), envelope.PublishedSha256);
        Assert.NotEqual(envelope.OriginalSha256, envelope.PublishedSha256);
    }

    [Fact]
    [Trait("Requirement", "RP-10")]
    public void Project_RedactedSpans_AreDeclaredInOrdinalOrder()
    {
        var body = "abcdefghij";
        var (view, reader, _) = ProjectionPackageFactory.PackageWithSecret(
            "Acme.Orders/Acme.Orders.csproj",
            "Acme.Orders/appsettings.json",
            body,
            new SourceSpanDto(1, 6, 1, 7));
        var laterFirst = view.Document.Diagnostics.Records;
        var extra = laterFirst.Add(
            new DiagnosticRecordDto(
                "suspected-secret",
                "1:2-1:3 Password=***",
                view.Document.Documents[0].Identity.Id));
        view = PublishedPackageView.From(view.Document with { Diagnostics = new DiagnosticsEnvelope(extra) });

        var envelopeFragment = Assert.Single(
            SourceProjector.Project(view, reader),
            fragment => fragment.CanonicalKey.EndsWith(".meta.json", StringComparison.Ordinal));
        var envelope = CanonicalJson.Read<RedactionEnvelopeDto>(envelopeFragment.ReadPayload().AsSpan());

        Assert.Equal(2, envelope.RedactedSpans.Length);
        Assert.Equal(2, envelope.RedactedSpans[0].StartColumn);
        Assert.Equal(6, envelope.RedactedSpans[1].StartColumn);
        Assert.True(
            (envelope.RedactedSpans[0].StartLine, envelope.RedactedSpans[0].StartColumn)
                .CompareTo((envelope.RedactedSpans[1].StartLine, envelope.RedactedSpans[1].StartColumn)) < 0);
    }

    [Fact]
    [Trait("Requirement", "RP-10")]
    public void Project_UnredactedDocument_HasNoEnvelope()
    {
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", "class Program;"u8.ToArray()));

        var fragments = SourceProjector.Project(view, reader);

        Assert.Single(fragments);
        Assert.DoesNotContain(
            fragments,
            fragment => fragment.CanonicalKey.EndsWith(".meta.json", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-10")]
    public void Fragment_DeclaresRedactedTrueAndOrdinalSpans()
    {
        var original = Encoding.UTF8.GetBytes("abcdef").ToImmutableArray();
        var published = SecretRedactor.Redact(original, [new SourceSpanDto(1, 2, 1, 4)]);
        var fragment = RedactionEnvelope.Fragment(
            "id1:document;path=a",
            "source/acme.orders/a.cs",
            original,
            published,
            [new SourceSpanDto(1, 4, 1, 4), new SourceSpanDto(1, 2, 1, 3)]);

        var envelope = CanonicalJson.Read<RedactionEnvelopeDto>(fragment.Payload.AsSpan());

        Assert.True(envelope.Redacted);
        Assert.Equal([(2, 3), (4, 4)], envelope.RedactedSpans.Select(span => (span.StartColumn, span.EndColumn)));
        Assert.Equal("source/acme.orders/a.cs.meta.json", fragment.CanonicalKey);
    }

    [Fact]
    [Trait("Requirement", "RP-10")]
    public void Fragment_HashesMatchOriginalAndPublishedBytes()
    {
        var original = Encoding.UTF8.GetBytes("secret-bytes").ToImmutableArray();
        var published = SecretRedactor.Marker;
        var fragment = RedactionEnvelope.Fragment(
            "doc",
            "source/acme.orders/secret.txt",
            original,
            published,
            [new SourceSpanDto(1, 1, 1, original.Length)]);
        var envelope = CanonicalJson.Read<RedactionEnvelopeDto>(fragment.Payload.AsSpan());

        Assert.Equal(Sha256(original), envelope.OriginalSha256);
        Assert.Equal(Sha256(published), envelope.PublishedSha256);
    }

    private static string Sha256(ImmutableArray<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan()));

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
