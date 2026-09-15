using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class ManifestBuilder
{
    public static ManifestEnvelope From(
        ManifestContext context,
        ImmutableArray<StagedFragment> payloads,
        PublishedPackageView view,
        ProvenanceDto? provenance = null)
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

        var effectiveProvenance = provenance ?? ProvenanceDto.Current();
        return new ManifestEnvelope(
            effectiveProvenance.SchemaVersion,
            effectiveProvenance.TaxonomyVersion,
            effectiveProvenance.ObservationSchemaVersion,
            context.SolutionKey,
            context.SolutionFileName,
            artifacts.ToImmutable(),
            effectiveProvenance);
    }

    /// <summary>
    /// An artifact's own real top-level entry count: a JSON array's length; a JSON object whose every
    /// top-level property is itself an array has those arrays summed (matching how the compound
    /// fact-family bundles, and the single-array <c>{"records": [...]}</c> envelopes, hold their records);
    /// one for anything else, including a singleton envelope that happens to carry an incidental array
    /// field (such as run-certification's list of free-text reasons, which is not a record collection).
    /// Also used by <see cref="Validation.PackageValidator"/> to recompute an artifact's count when
    /// checking it against the manifest.
    /// </summary>
    internal static int CountTopLevelEntries(ReadOnlySpan<byte> payload)
    {
        try
        {
            var node = JsonNode.Parse(payload);
            return node switch
            {
                JsonArray array => array.Count,
                JsonObject { Count: > 0 } obj when obj.All(static property => property.Value is JsonArray) =>
                    obj.Sum(static property => ((JsonArray)property.Value!).Count),
                _ => 1,
            };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // Source documents are indivisible artifacts, not package JSON. JsonNode can defer duplicate-
            // property validation until object enumeration, so treat either parse shape failure as the
            // same singleton-document case.
            return 1;
        }
    }

    private static string Stem(string fragmentKey) =>
        fragmentKey.EndsWith(".json", StringComparison.Ordinal)
            ? fragmentKey[..^".json".Length]
            : fragmentKey;
}
