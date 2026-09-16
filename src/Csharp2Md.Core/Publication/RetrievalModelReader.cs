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

            var tables = $"solutions/{entry.Id.Value}/tables";
            var entities = ReadTable(artifacts, $"{tables}/entities.000000.json", identities[0].CanonicalKey);
            var variants = ReadTable(artifacts, $"{tables}/variants.000000.json", identities[0].CanonicalKey);
            var cycles = ReadTable(artifacts, $"{tables}/cycles.000000.json", identities[0].CanonicalKey);
            var payload = Read<DependencyPayload>(artifacts, outgoing.ArtifactPath);
            var evidenceRows = ReadEvidenceTable(artifacts, indexes[NavigationIndexKind.Evidence]);
            if (!payload.Evidence.SequenceEqual(evidenceRows.Select(item => item.CanonicalKey), StringComparer.Ordinal))
                throw new PackageCorruptionException(indexes[NavigationIndexKind.Evidence]);
            var dependencies = ExpandDependencies(payload, identities[0].CanonicalKey, outgoing.ArtifactPath, entities, variants);
            var measures = ExpandMeasures(
                Read<ImmutableArray<ScopeMeasures>>(artifacts, measuresIndex.ArtifactPath),
                measuresIndex.ArtifactPath,
                entities,
                cycles);
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

    private static ImmutableArray<EvidenceRecord> ReadEvidenceTable(IReadOnlyDictionary<string, ImmutableArray<byte>> artifacts, string indexPath)
    {
        var index = Read<EvidenceIndexData>(artifacts, indexPath);
        var rows = ImmutableArray.CreateBuilder<EvidenceRecord>();
        foreach (var shard in index.Shards)
        {
            if (shard.FirstOrdinal != rows.Count)
            {
                throw new PackageCorruptionException(indexPath);
            }

            var records = Read<ImmutableArray<EvidenceRecord>>(artifacts, shard.ArtifactPath);
            if (records.Length != shard.Count)
            {
                throw new PackageCorruptionException(shard.ArtifactPath);
            }

            rows.AddRange(records);
        }

        return rows.ToImmutable();
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

    private static IReadOnlyDictionary<string, string> ReadTable(
        IReadOnlyDictionary<string, ImmutableArray<byte>> artifacts,
        string path,
        string solutionKey)
    {
        var keys = Read<ImmutableArray<string>>(artifacts, path);
        if (!keys.SequenceEqual(keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new PackageCorruptionException(path);
        }

        try
        {
            return LocalTableBuilder.Build(solutionKey, keys).Handles
                .ToDictionary(pair => pair.Value.Value, pair => pair.Key, StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            throw new PackageCorruptionException(path);
        }
    }

    private static ImmutableArray<ScopeMeasures> ExpandMeasures(
        ImmutableArray<ScopeMeasures> measures,
        string path,
        IReadOnlyDictionary<string, string> entities,
        IReadOnlyDictionary<string, string> cycles)
    {
        try
        {
            return measures.Select(measure => new ScopeMeasures(
                measure.Scope,
                new EntityHandle(entities[measure.Entity.Value]),
                measure.FanIn,
                measure.FanOut,
                measure.CrossComponentEdges,
                measure.Cycles.Select(cycle => new CycleHandle(cycles[cycle.Value])).ToImmutableArray(),
                measure.ReverseImpact.Select(target => new ImpactTarget(new EntityHandle(entities[target.Entity.Value]), target.Depth)).ToImmutableArray(),
                measure.Gaps)).ToImmutableArray();
        }
        catch (Exception exception) when (exception is KeyNotFoundException or ArgumentException)
        {
            throw new PackageCorruptionException(path);
        }
    }

    private static ImmutableArray<AggregatedDependency> ExpandDependencies(
        DependencyPayload payload,
        string solutionKey,
        string path,
        IReadOnlyDictionary<string, string> entities,
        IReadOnlyDictionary<string, string> variants)
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
                if (!entities.ContainsKey(relation.SourceCanonicalKey)
                    || !entities.ContainsKey(relation.TargetCanonicalKey)
                    || string.IsNullOrWhiteSpace(relation.Category)
                    || relation.Evidence.Any(handle => !evidence.ContainsKey(handle.Value)))
                    throw new PackageCorruptionException(path);
            }

            return payload.Dependencies.Select(dependency => new AggregatedDependency(
                dependency.Scope,
                new EntityHandle(entities[dependency.Source.Value]),
                new EntityHandle(entities[dependency.Target.Value]),
                dependency.Category,
                dependency.Nature,
                dependency.OccurrenceCount,
                dependency.Variants.Select(handle => new VariantHandle(variants[handle.Value])).ToImmutableArray(),
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
