using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using System.Text.Json;

namespace Csharp2Md.Core.Publication;

internal sealed class PackageCorruptionException : InvalidOperationException
{
    internal PackageCorruptionException(string artifact) : base($"package-corruption: '{artifact}'.") => Artifact = artifact;
    internal string Artifact { get; }
}

internal static class RetrievalModelReader
{
    internal static RetrievalModel Read(IReadOnlyDictionary<string, ImmutableArray<byte>> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        var manifest = Read<PackageManifest>(artifacts, "manifest.json");
        var solutions = ImmutableArray.CreateBuilder<SolutionRetrievalModel>();
        foreach (var entry in manifest.Solutions.OrderBy(solution => solution.Id.Value, StringComparer.Ordinal))
        {
            var indexes = ResolveIndexes(entry);
            var identities = Read<ImmutableArray<SolutionIdentity>>(artifacts, indexes[NavigationIndexKind.Identity]);
            if (identities.Length != 1)
            {
                throw new PackageCorruptionException(indexes[NavigationIndexKind.Identity]);
            }

            solutions.Add(new SolutionRetrievalModel(
                identities[0],
                Read<ImmutableArray<EntityHandle>>(artifacts, indexes[NavigationIndexKind.Roots]),
                Read<ImmutableArray<AggregatedDependency>>(artifacts, indexes[NavigationIndexKind.Outgoing]),
                Read<ImmutableArray<ScopeMeasures>>(artifacts, indexes[NavigationIndexKind.Measures])));
        }

        return new RetrievalModel(solutions.ToImmutable());
    }

    internal static void VerifyMarkdown(IReadOnlyDictionary<string, ImmutableArray<byte>> artifacts)
    {
        var manifest = Read<PackageManifest>(artifacts, "manifest.json");
        var expected = MarkdownRenderer.Render(Read(artifacts), manifest);
        foreach (var artifact in expected)
        {
            if (!artifacts.TryGetValue(artifact.Path.Value, out var actual) || !actual.AsSpan().SequenceEqual(artifact.Payload.AsSpan()))
            {
                throw new PackageCorruptionException(artifact.Path.Value);
            }
        }
    }

    private static IReadOnlyDictionary<NavigationIndexKind, string> ResolveIndexes(SolutionManifestEntry entry)
    {
        var indexes = new Dictionary<NavigationIndexKind, string>();
        foreach (var index in entry.Indexes)
        {
            if (!indexes.TryAdd(index.Kind, index.EntryPath))
            {
                throw new PackageCorruptionException($"manifest.json#{entry.Id.Value}/{index.Kind}");
            }
        }

        foreach (var kind in Enum.GetValues<NavigationIndexKind>())
        {
            if (!indexes.ContainsKey(kind))
            {
                throw new PackageCorruptionException($"manifest.json#{entry.Id.Value}/{kind}");
            }
        }

        return indexes;
    }

    private static T Read<T>(IReadOnlyDictionary<string, ImmutableArray<byte>> artifacts, string path)
    {
        if (!artifacts.TryGetValue(path, out var payload))
        {
            throw new PackageCorruptionException(path);
        }

        try { return CanonicalJson.Read<T>(payload.AsSpan()); }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
        {
            throw new PackageCorruptionException(path);
        }
    }
}
