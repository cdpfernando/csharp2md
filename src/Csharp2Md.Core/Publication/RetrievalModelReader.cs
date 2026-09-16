using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.PackageBuilding.Identity;
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

            var outgoing = Read<NavigationIndexData>(artifacts, indexes[NavigationIndexKind.Outgoing]);
            var measuresIndex = Read<NavigationIndexData>(artifacts, indexes[NavigationIndexKind.Measures]);
            var prefix = $"solutions/{entry.Id.Value}/measures";
            if (outgoing.ArtifactPath != $"{prefix}/dependencies.000000.json")
            {
                throw new PackageCorruptionException(indexes[NavigationIndexKind.Outgoing]);
            }
            if (measuresIndex.ArtifactPath != $"{prefix}/summary.json")
            {
                throw new PackageCorruptionException(indexes[NavigationIndexKind.Measures]);
            }

            var payload = Read<DependencyPayload>(artifacts, outgoing.ArtifactPath);
            var evidenceRows = Read<ImmutableArray<EvidenceRecord>>(artifacts, indexes[NavigationIndexKind.Evidence]);
            if (!payload.Evidence.SequenceEqual(evidenceRows.Select(item => item.CanonicalKey), StringComparer.Ordinal))
                throw new PackageCorruptionException(indexes[NavigationIndexKind.Evidence]);
            var dependencies = ExpandDependencies(payload, identities[0].CanonicalKey, outgoing.ArtifactPath);
            var measures = Read<ImmutableArray<ScopeMeasures>>(artifacts, measuresIndex.ArtifactPath);
            VerifyIndex(NavigationIndexKind.Outgoing, MachineArtifactWriter.BuildDependencyIndex(outgoing.ArtifactPath, dependencies, static dependency => dependency.Source.Value));
            VerifyIndex(NavigationIndexKind.Incoming, MachineArtifactWriter.BuildDependencyIndex(outgoing.ArtifactPath, dependencies, static dependency => dependency.Target.Value));
            VerifyIndex(NavigationIndexKind.Contracts, MachineArtifactWriter.BuildDependencyIndex(outgoing.ArtifactPath, dependencies, static dependency => dependency.Source.Value, DependencyCategory.Contract));
            VerifyIndex(NavigationIndexKind.Persistence, MachineArtifactWriter.BuildDependencyIndex(outgoing.ArtifactPath, dependencies, static dependency => dependency.Source.Value, DependencyCategory.Persistence));
            VerifyIndex(NavigationIndexKind.Measures, MachineArtifactWriter.BuildMeasuresIndex(measuresIndex.ArtifactPath, measures));
            solutions.Add(new SolutionRetrievalModel(
                identities[0],
                Read<ImmutableArray<EntityHandle>>(artifacts, indexes[NavigationIndexKind.Roots]),
                dependencies,
                measures));

            void VerifyIndex(NavigationIndexKind kind, NavigationIndexData expected)
            {
                var path = indexes[kind];
                var actual = Read<NavigationIndexData>(artifacts, path);
                if (!CanonicalJson.Write(actual).AsSpan().SequenceEqual(CanonicalJson.Write(expected).AsSpan()))
                {
                    throw new PackageCorruptionException(path);
                }
            }
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

    private static ImmutableArray<AggregatedDependency> ExpandDependencies(DependencyPayload payload, string solutionKey, string path)
    {
        try
        {
            var relationKeys = payload.Relations.Select(relation => relation.CanonicalKey).ToArray();
            var evidenceKeys = payload.Evidence.ToArray();
            if (!relationKeys.SequenceEqual(relationKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)
                || !evidenceKeys.SequenceEqual(evidenceKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new PackageCorruptionException(path);
            }

            var relations = LocalTableBuilder.Build(solutionKey, relationKeys).Handles
                .ToDictionary(pair => pair.Value.Value, pair => pair.Key, StringComparer.Ordinal);
            var evidence = LocalTableBuilder.Build(solutionKey, evidenceKeys).Handles
                .ToDictionary(pair => pair.Value.Value, pair => pair.Key, StringComparer.Ordinal);
            foreach (var relation in payload.Relations)
            {
                if (string.IsNullOrWhiteSpace(relation.SourceCanonicalKey)
                    || string.IsNullOrWhiteSpace(relation.TargetCanonicalKey)
                    || string.IsNullOrWhiteSpace(relation.Category)
                    || relation.Evidence.Any(handle => !evidence.ContainsKey(handle.Value)))
                    throw new PackageCorruptionException(path);
            }

            return payload.Dependencies.Select(dependency => new AggregatedDependency(
                dependency.Scope,
                dependency.Source,
                dependency.Target,
                dependency.Category,
                dependency.Nature,
                dependency.OccurrenceCount,
                dependency.Variants,
                dependency.Relations.Select(handle => new RelationHandle(relations[handle.Value])).ToImmutableArray(),
                dependency.Evidence.Select(handle => new EvidenceHandle(evidence[handle.Value])).ToImmutableArray()))
                .ToImmutableArray();
        }
        catch (Exception exception) when (exception is KeyNotFoundException or ArgumentException)
        {
            throw new PackageCorruptionException(path);
        }
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
