using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

/// <summary>
/// Bounds <c>manifest.json</c> under the declared per-artifact ceiling (F6/GCPC-038): the manifest's own
/// size is linear in the artifact count (~190 bytes per <see cref="ManifestEntry"/>), which F1's compound-
/// family sharding multiplied, so a package with enough artifacts pushes the manifest itself past the
/// ceiling it is supposed to help enforce. When the whole manifest fits, its shape is unchanged from
/// before this fix (byte-identical for every small package). When it does not, the manifest splits into a
/// small, fixed-shape root (<see cref="PackagePublisher.ManifestKey"/>, carrying <see
/// cref="ManifestEnvelope.Provenance"/>, the solution identity and the version axes unchanged) plus one or
/// more <c>manifest/parts.lNN.NNNN.json</c> shards, each a flat JSON array of ordered <see
/// cref="ManifestEntry"/> records. If the leaf-part pointers would themselves overflow the root, they
/// are partitioned into another level until the root fits. Stable canonical-key ordering and stable
/// level/ordinal names make two runs over the same document agree (GCPC-042).
/// </summary>
internal static class ManifestSharder
{
    /// <summary>The <see cref="ManifestEntry.Role"/> value for a root entry that points at a
    /// <c>manifest/parts.&lt;bucket&gt;.json</c> shard rather than naming a real published artifact.
    /// <see cref="Validation.PackageValidator"/> and <see cref="FactualPackageReader"/> recognize it to
    /// resolve the manifest's real entries before doing anything else with them.</summary>
    internal const string PartRole = "manifest-part";

    /// <summary>
    /// Produces the fragment(s) to publish for <paramref name="manifest"/>: one <see
    /// cref="ArtifactRole.Manifest"/> fragment at <see cref="PackagePublisher.ManifestKey"/>, plus any
    /// <c>manifest/parts.*.json</c> shards the root needed to stay under <paramref name="ceilingBytes"/>.
    /// </summary>
    internal static ImmutableArray<StagedFragment> ToFragments(ManifestEnvelope manifest, int ceilingBytes)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var unsplitBytes = CanonicalJson.Write(manifest);
        if (unsplitBytes.Length <= ceilingBytes || manifest.Artifacts.IsDefaultOrEmpty)
        {
            return [new StagedFragment(ArtifactRole.Manifest, PackagePublisher.ManifestKey, unsplitBytes)];
        }

        // Tests and callers may deliberately use byte-sized ceilings to force one record family to
        // split. The manifest's fixed provenance/identity envelope cannot itself be partitioned; in that
        // artificial case preserve the pre-F6 unsplit behavior instead of entering a non-shrinking loop.
        if (CanonicalJson.Write(manifest with { Artifacts = [] }).Length > ceilingBytes)
        {
            return [new StagedFragment(ArtifactRole.Manifest, PackagePublisher.ManifestKey, unsplitBytes)];
        }

        var result = ImmutableArray.CreateBuilder<StagedFragment>();
        var entries = manifest.Artifacts
            .OrderBy(static entry => entry.CanonicalKey, StringComparer.Ordinal)
            .ToImmutableArray();
        var level = 0;
        while (CanonicalJson.Write(manifest with { Artifacts = entries }).Length > ceilingBytes)
        {
            var pointers = WriteParts(entries, ceilingBytes, level++, result);
            if (pointers.Length >= entries.Length)
            {
                throw new PublicationRejectedException(
                    "record-exceeds-ceiling",
                    $"manifest index cannot fit within {ceilingBytes} bytes");
            }

            entries = pointers;
        }

        var root = manifest with { Artifacts = entries };
        result.Add(new StagedFragment(
            ArtifactRole.Manifest,
            PackagePublisher.ManifestKey,
            CanonicalJson.Write(root)));
        return result.ToImmutable();
    }

    /// <summary>
    /// Resolves <paramref name="manifest"/> to the full, real set of <see cref="ManifestEntry"/> records:
    /// unchanged when it was never split, or the root's part-pointer entries plus every entry read back
    /// from each part (via <paramref name="readPart"/>) when it was. The pointer entries themselves stay
    /// in the resolved list -- they are real published artifacts too, and need to keep validating like any
    /// other. Used by every reader of an already-published manifest (<see cref="FactualPackageReader"/>,
    /// <see cref="Validation.PackageValidator"/>, <see cref="PublicationPipeline"/>'s pre-write check) so
    /// none of them need their own copy of this resolution.
    /// </summary>
    internal static ManifestEnvelope Resolve(ManifestEnvelope manifest, Func<string, ImmutableArray<byte>> readPart)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(readPart);

        if (manifest.Artifacts.IsDefaultOrEmpty || !manifest.Artifacts.Any(static entry => entry.Role == PartRole))
        {
            return manifest;
        }

        var merged = ImmutableArray.CreateBuilder<ManifestEntry>();
        var visitedParts = new HashSet<string>(StringComparer.Ordinal);
        Expand(manifest.Artifacts);
        return manifest with { Artifacts = merged.ToImmutable() };

        void Expand(ImmutableArray<ManifestEntry> entries)
        {
            foreach (var entry in entries)
            {
                merged.Add(entry);
                if (entry.Role != PartRole)
                {
                    continue;
                }

                if (!visitedParts.Add(entry.Path))
                {
                    throw new PublicationRejectedException("manifest-part-cycle", entry.Path);
                }

                var bytes = readPart(entry.Path);
                Expand(Validation.PackageValidator.ReadPayloadOrThrow<ImmutableArray<ManifestEntry>>(
                    bytes.AsSpan(), entry.Path));
            }
        }
    }

    private static ImmutableArray<ManifestEntry> WriteParts(
        ImmutableArray<ManifestEntry> entries,
        int ceilingBytes,
        int level,
        ImmutableArray<StagedFragment>.Builder fragments)
    {
        var groups = new List<ImmutableArray<ManifestEntry>>();
        var current = ImmutableArray.CreateBuilder<ManifestEntry>();
        foreach (var entry in entries)
        {
            current.Add(entry);
            if (SerializePart(current).Length <= ceilingBytes)
            {
                continue;
            }

            current.RemoveAt(current.Count - 1);
            if (current.Count == 0)
            {
                throw new PublicationRejectedException(
                    "record-exceeds-ceiling",
                    $"manifest entry '{entry.Path}' exceeds {ceilingBytes} bytes");
            }

            groups.Add(current.ToImmutable());
            current.Clear();
            current.Add(entry);
        }

        if (current.Count > 0)
        {
            groups.Add(current.ToImmutable());
        }

        var pointers = ImmutableArray.CreateBuilder<ManifestEntry>();
        for (var index = 0; index < groups.Count; index++)
        {
            var partBytes = SerializePart(groups[index]);
            var partKey = $"manifest/parts.l{level:D2}.{index:D4}.json";
            fragments.Add(new StagedFragment(ArtifactRole.Payload, partKey, partBytes));
            pointers.Add(new ManifestEntry(Stem(partKey), PartRole, groups[index].Length, partBytes.Length, partKey));
        }

        return pointers.ToImmutable();
    }

    private static ImmutableArray<byte> SerializePart(IReadOnlyList<ManifestEntry> entries) =>
        CanonicalJson.Write(entries.ToImmutableArray());

    private static string Stem(string key) =>
        key.EndsWith(".json", StringComparison.Ordinal) ? key[..^".json".Length] : key;
}
