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
        var indexes = manifest.Indexes.ToDictionary(index => index.Name, index => index.Path, StringComparer.Ordinal);
        var solutions = Read<ImmutableArray<SolutionNavigation>>(artifacts, Required(indexes, "identity"));
        var dependencies = Read<ImmutableArray<AggregatedDependency>>(artifacts, Required(indexes, "outgoing"));
        var measures = Read<ImmutableArray<ScopeMeasures>>(artifacts, Required(indexes, "measures"));
        var navigation = Read<NavigationIndexes>(artifacts, Required(indexes, "evidence"));
        return new RetrievalModel(solutions, dependencies, measures, navigation);
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

    private static string Required(IReadOnlyDictionary<string, string> indexes, string name) =>
        indexes.TryGetValue(name, out var path) ? path : throw new PackageCorruptionException($"manifest.json#{name}");

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
