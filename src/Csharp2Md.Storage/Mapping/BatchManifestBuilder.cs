using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class BatchManifestBuilder
{
    public static BatchManifestEnvelope From(BatchView batch, ImmutableArray<StagedFragment> composition)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var solutions = batch.Solutions.IsDefaultOrEmpty
            ? ImmutableArray<BatchManifestSolutionEntry>.Empty
            : batch.Solutions
                .OrderBy(static record => record.Identity.Value, StringComparer.Ordinal)
                .Select(ToSolutionEntry)
                .ToImmutableArray();

        var artifacts = composition.IsDefaultOrEmpty
            ? ImmutableArray<BatchManifestArtifactEntry>.Empty
            : composition
                .Where(static fragment => fragment.Role == ArtifactRole.Payload)
                .OrderBy(static fragment => fragment.CanonicalKey, StringComparer.Ordinal)
                .Select(static fragment => new BatchManifestArtifactEntry(
                    fragment.CanonicalKey,
                    "payload",
                    CountEntries(fragment)))
                .ToImmutableArray();

        return new BatchManifestEnvelope(
            TaxonomyVersions.Initial.SchemaVersion,
            batch.Complete,
            batch.IncompleteScopeReason,
            solutions,
            artifacts);
    }

    internal static string PackageDirectoryName(string identity)
    {
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
        return "s-" + hex;
    }

    private static BatchManifestSolutionEntry ToSolutionEntry(BatchSolutionRecord record) =>
        new(
            record.Identity.Value,
            record.SolutionFileName,
            PackageDirectoryName(record.Identity.Value),
            record.Status == PublicationStatus.Committed ? "committed" : "unpublished",
            record.FailingStage);

    private static int CountEntries(StagedFragment fragment)
    {
        var payload = fragment.IsDeferred ? fragment.ReadPayload() : fragment.Payload;
        if (payload.IsDefaultOrEmpty)
        {
            return 0;
        }

        try
        {
            var node = JsonNode.Parse(payload.AsSpan());
            return node is JsonArray array ? array.Count : 1;
        }
        catch (JsonException)
        {
            return 1;
        }
    }
}
