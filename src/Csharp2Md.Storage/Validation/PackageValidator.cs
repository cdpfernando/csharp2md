using System.Text.Json;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Validation;

public sealed record ValidationReport(WireDocument Document, ImmutableArray<QuarantineRecordDto> Quarantine);

public static class PackageValidator
{
    public static ValidationReport Validate(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new ValidationReport(document, document.Quarantine);
    }

    public static T ReadPayloadOrThrow<T>(ReadOnlySpan<byte> utf8, string artifactKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactKey);

        try
        {
            return CanonicalJson.Read<T>(utf8);
        }
        catch (JsonException)
        {
            throw new PublicationRejectedException("schema", artifactKey);
        }
    }
}
