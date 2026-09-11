using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests;

internal static class PublishedManifestTestData
{
    private const string PartRole = "manifest-part";

    internal static ManifestEnvelope Read(CommittedPublication publication)
    {
        var fragments = publication.ArtifactsInPublicationOrder.ToDictionary(
            static fragment => fragment.CanonicalKey,
            StringComparer.Ordinal);
        var root = CanonicalJson.Read<ManifestEnvelope>(fragments["manifest.json"].Payload.AsSpan());
        var entries = ImmutableArray.CreateBuilder<ManifestEntry>();
        Expand(root.Artifacts);
        return root with { Artifacts = entries.ToImmutable() };

        void Expand(ImmutableArray<ManifestEntry> level)
        {
            foreach (var entry in level)
            {
                entries.Add(entry);
                if (entry.Role == PartRole)
                {
                    Expand(CanonicalJson.Read<ImmutableArray<ManifestEntry>>(fragments[entry.Path].Payload.AsSpan()));
                }
            }
        }
    }
}
