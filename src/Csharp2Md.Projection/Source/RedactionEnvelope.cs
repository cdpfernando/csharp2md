using System.Security.Cryptography;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Source;

internal static class RedactionEnvelope
{
    internal static StagedFragment Fragment(
        string documentId,
        string artifactKey,
        ImmutableArray<byte> original,
        ImmutableArray<byte> published,
        ImmutableArray<SourceSpanDto> spans)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactKey);

        var ordered = spans.IsDefaultOrEmpty
            ? ImmutableArray<SourceSpanDto>.Empty
            : spans.OrderBy(static span => span.StartLine)
                .ThenBy(static span => span.StartColumn)
                .ThenBy(static span => span.EndLine)
                .ThenBy(static span => span.EndColumn)
                .ToImmutableArray();

        var dto = new RedactionEnvelopeDto(
            documentId,
            artifactKey,
            Redacted: true,
            ordered,
            Sha256(original),
            Sha256(published));
        return new StagedFragment(
            ArtifactRole.Payload,
            artifactKey + ".meta.json",
            CanonicalJson.Write(dto));
    }

    internal static string Sha256(ImmutableArray<byte> bytes)
    {
        var data = bytes.IsDefault ? ReadOnlySpan<byte>.Empty : bytes.AsSpan();
        return Convert.ToHexStringLower(SHA256.HashData(data));
    }
}
