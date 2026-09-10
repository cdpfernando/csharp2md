using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class ManifestBuilder
{
    public static ManifestEnvelope From(
        ManifestContext context,
        ImmutableArray<StagedFragment> payloads,
        PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(view);

        // The plan already knows the real count for every artifact it planned (including a family split
        // across shards); a projection artifact (catalogs, postings, labels, source copies) is not part
        // of the plan at all, so its count is derived from its own bytes instead of defaulting to zero
        // (GCPC-061).
        var plannedCounts = view.Plan.Artifacts.ToDictionary(
            static artifact => artifact.ArtifactKey,
            static artifact => artifact.Count,
            StringComparer.Ordinal);

        var artifacts = ImmutableArray.CreateBuilder<ManifestEntry>();
        foreach (var fragment in payloads)
        {
            if (fragment.Role != ArtifactRole.Payload)
            {
                continue;
            }

            // A deferred fragment's payload can be materialized only once, and the transactional store
            // still owns that single read for writing the file to disk -- the manifest must not consume
            // it here. Its count falls back to the plan (zero for a non-plan deferred artifact, such as a
            // raw source copy, which is not itself a JSON record set).
            int count;
            long byteSize;
            if (fragment.IsDeferred)
            {
                plannedCounts.TryGetValue(fragment.CanonicalKey, out count);
                byteSize = 0;
            }
            else
            {
                var payload = fragment.Payload;
                byteSize = payload.Length;
                count = plannedCounts.TryGetValue(fragment.CanonicalKey, out var plannedCount)
                    ? plannedCount
                    : CountTopLevelEntries(payload.AsSpan());
            }

            artifacts.Add(new ManifestEntry(
                Stem(fragment.CanonicalKey),
                "payload",
                count,
                byteSize,
                fragment.CanonicalKey));
        }

        var versions = TaxonomyVersions.Initial;
        return new ManifestEnvelope(
            versions.SchemaVersion,
            versions.TaxonomyVersion,
            versions.ObservationSchemaVersion,
            context.SolutionKey,
            context.SolutionFileName,
            artifacts.ToImmutable(),
            ProvenanceDto.Current());
    }

    /// <summary>
    /// An artifact's own real top-level entry count: a JSON array's length; a JSON object's own arrays
    /// summed (matching how the compound fact-family bundles concatenate their sub-arrays); one for
    /// anything else, including a single-record envelope.
    /// </summary>
    private static int CountTopLevelEntries(ReadOnlySpan<byte> payload)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return 1;
        }

        switch (node)
        {
            case JsonArray array:
                return array.Count;
            case JsonObject obj:
                var sum = 0;
                var sawArray = false;
                foreach (var property in obj)
                {
                    if (property.Value is JsonArray family)
                    {
                        sawArray = true;
                        sum += family.Count;
                    }
                }

                return sawArray ? sum : 1;
            default:
                return 1;
        }
    }

    private static string Stem(string fragmentKey) =>
        fragmentKey.EndsWith(".json", StringComparison.Ordinal)
            ? fragmentKey[..^".json".Length]
            : fragmentKey;
}
