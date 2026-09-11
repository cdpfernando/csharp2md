using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class ManifestSharderTests
{
    [Fact]
    [Trait("Requirement", "GCPC-038")]
    [Trait("Requirement", "GCPC-062")]
    public void ToFragments_LargeManifest_BoundsEveryLevelAndResolvesEveryOriginalEntry()
    {
        const int ceilingBytes = 2048;
        var originalEntries = Enumerable.Range(0, 500)
            .Select(index => new ManifestEntry(
                $"facts/generated/{index:D4}",
                "payload",
                1,
                128,
                $"facts/generated/{index:D4}.json"))
            .ToImmutableArray();
        var manifest = new ManifestEnvelope(
            2,
            2,
            1,
            "s-test",
            "Acme.sln",
            originalEntries,
            ProvenanceDto.Current());

        var fragments = ManifestSharder.ToFragments(manifest, ceilingBytes);

        Assert.All(fragments, fragment => Assert.True(
            fragment.Payload.Length <= ceilingBytes,
            $"'{fragment.CanonicalKey}' is {fragment.Payload.Length} bytes."));
        var root = CanonicalJson.Read<ManifestEnvelope>(
            Assert.Single(fragments, static fragment => fragment.CanonicalKey == "manifest.json").Payload.AsSpan());
        Assert.All(root.Artifacts, static entry => Assert.Equal(ManifestSharder.PartRole, entry.Role));

        var byKey = fragments.ToDictionary(static fragment => fragment.CanonicalKey, StringComparer.Ordinal);
        var resolved = ManifestSharder.Resolve(root, path => byKey[path].Payload);
        Assert.Contains(resolved.Artifacts, static entry => entry.Role == ManifestSharder.PartRole);
        Assert.Equal(
            originalEntries.Select(static entry => entry.CanonicalKey),
            resolved.Artifacts
                .Where(static entry => entry.Role != ManifestSharder.PartRole)
                .Select(static entry => entry.CanonicalKey));
    }
}
