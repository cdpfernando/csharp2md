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

        var counts = view.Slots.ToDictionary(
            static slot => slot.CanonicalKey,
            static slot => slot.Count,
            StringComparer.Ordinal);

        var artifacts = ImmutableArray.CreateBuilder<ManifestEntry>();
        foreach (var fragment in payloads)
        {
            if (fragment.Role != ArtifactRole.Payload)
            {
                continue;
            }

            counts.TryGetValue(fragment.CanonicalKey, out var count);
            artifacts.Add(new ManifestEntry(
                Stem(fragment.CanonicalKey),
                "payload",
                count,
                fragment.CanonicalKey));
        }

        var versions = TaxonomyVersions.Initial;
        return new ManifestEnvelope(
            versions.SchemaVersion,
            versions.TaxonomyVersion,
            versions.ObservationSchemaVersion,
            context.SolutionKey,
            context.SolutionFileName,
            artifacts.ToImmutable());
    }

    private static string Stem(string fragmentKey) =>
        fragmentKey.EndsWith(".json", StringComparison.Ordinal)
            ? fragmentKey[..^".json".Length]
            : fragmentKey;
}
