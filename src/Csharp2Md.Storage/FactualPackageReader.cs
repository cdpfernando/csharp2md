using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage;

/// <summary>
/// Reads a published package by enumerating its manifest rather than a fixed set of keys (AD-023): a
/// family split across shards is transparently merged back into one array, no file outside the manifest
/// is ever opened, and every manifest artifact the core wire document does not account for is returned as
/// a projection fragment alongside it.
/// </summary>
public static class FactualPackageReader
{
    public static PackageReadResult Read(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        if (!Directory.Exists(packageDirectory)
            || !File.Exists(ManifestPath(packageDirectory)))
        {
            throw new PublicationRejectedException("not-a-package", packageDirectory);
        }

        var manifestBytes = File.ReadAllBytes(ManifestPath(packageDirectory));
        var manifest = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(manifestBytes, PackagePublisher.ManifestKey);

        var consumed = new HashSet<string>(StringComparer.Ordinal) { PackagePublisher.ManifestKey };
        manifest = ManifestSharder.Resolve(
            manifest, path => File.ReadAllBytes(ShardPath(packageDirectory, path)).ToImmutableArray());
        foreach (var pointer in manifest.Artifacts.Where(static entry => entry.Role == ManifestSharder.PartRole))
        {
            consumed.Add(pointer.Path);
        }

        var document = LoadDocument(packageDirectory, manifest, consumed);
        var report = PackageValidator.Validate(document);

        var projections = manifest.Artifacts
            .Where(entry => !consumed.Contains(entry.Path))
            .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
            .Select(entry => new StagedFragment(
                ArtifactRole.Payload,
                entry.Path,
                File.ReadAllBytes(ShardPath(packageDirectory, entry.Path)).ToImmutableArray()))
            .ToImmutableArray();

        return new PackageReadResult(
            DomainMapper.FromWire(report.Document),
            MapQuarantine(report.Quarantine),
            report.Document.Coverage,
            report.Document.RunCertification,
            projections);
    }

    private static WireDocument LoadDocument(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed)
    {
        var structural = ReadStructural(packageDirectory, manifest, consumed);
        var architecture = ReadArchitecture(packageDirectory, manifest, consumed);
        var contract = ReadContract(packageDirectory, manifest, consumed);
        var persistence = ReadPersistence(packageDirectory, manifest, consumed);
        var configuration = ReadConfiguration(packageDirectory, manifest, consumed);

        var observations = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ObservationDto>>(StringComparer.Ordinal);
        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            var records = ReadShardedArray<ObservationDto>(packageDirectory, manifest, consumed, "observations/" + kind.WireName + ".json");
            if (!records.IsDefaultOrEmpty)
            {
                observations[kind.WireName] = records;
            }
        }

        var confirmed = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ConfirmedRelationDto>>(StringComparer.Ordinal);
        foreach (var relation in TaxonomyTables.Default.Relations)
        {
            var records = ReadShardedArray<ConfirmedRelationDto>(
                packageDirectory, manifest, consumed, "relations/confirmed/" + relation.WireName + ".json");
            if (!records.IsDefaultOrEmpty)
            {
                confirmed[relation.WireName] = records;
            }
        }

        var quarantineEnvelope = ReadQuarantine(packageDirectory, manifest, consumed);
        var coverage = ReadRequiredObject<CoverageEnvelope>(packageDirectory, manifest, consumed, "coverage.json");
        var certification = ReadRequiredObject<RunCertificationEnvelope>(packageDirectory, manifest, consumed, "run-certification.json");
        var diagnostics = ReadOptionalObject<DiagnosticsEnvelope>(packageDirectory, manifest, consumed, "diagnostics.json")
            ?? new DiagnosticsEnvelope([]);
        var measurements = ReadOptionalObject<MeasurementsEnvelope>(packageDirectory, manifest, consumed, "measurements.json")
            ?? new MeasurementsEnvelope([]);

        var registryPath = "contracts/taxonomy-registry.json";
        var registry = IsListed(manifest, consumed, registryPath)
            ? File.ReadAllBytes(ShardPath(packageDirectory, registryPath)).ToImmutableArray()
            : ImmutableArray<byte>.Empty;

        return new WireDocument(
            manifest,
            registry,
            structural?.Solutions ?? [],
            structural?.Projects ?? [],
            structural?.Documents ?? [],
            structural?.Symbols ?? [],
            architecture?.Components ?? [],
            architecture?.DeploymentUnits ?? [],
            architecture?.EntryPoints ?? [],
            architecture?.BoundaryOperations ?? [],
            architecture?.ExternalSystems ?? [],
            contract?.Contracts ?? [],
            contract?.ContractBindings ?? [],
            contract?.ContractRevisions ?? [],
            persistence?.DataStores ?? [],
            persistence?.DataObjects ?? [],
            persistence?.DataFields ?? [],
            persistence?.DataOperations ?? [],
            configuration?.ConfigurationBindings ?? [],
            observations.ToImmutable(),
            confirmed.ToImmutable(),
            ReadShardedArray<CandidateLinkDto>(packageDirectory, manifest, consumed, "relations/candidates.json"),
            ReadShardedArray<UnresolvedRecordDto>(packageDirectory, manifest, consumed, "relations/unresolved.json"),
            ReadShardedArray<OpenFrontierDto>(packageDirectory, manifest, consumed, "relations/frontiers.json"),
            quarantineEnvelope is null || quarantineEnvelope.Records.IsDefault
                ? []
                : quarantineEnvelope.Records,
            coverage,
            certification,
            diagnostics,
            measurements);
    }

    private static ImmutableArray<QuarantineRecord> MapQuarantine(ImmutableArray<QuarantineRecordDto> records)
    {
        if (records.IsDefaultOrEmpty)
        {
            return [];
        }

        return [.. records.Select(static dto => new QuarantineRecord(
            dto.RecordKind,
            dto.IdentityOrKey,
            dto.Gate,
            dto.Detail,
            dto.Payload))];
    }

    private static bool IsListed(ManifestEnvelope manifest, HashSet<string> consumed, string path)
    {
        if (!manifest.Artifacts.Any(entry => entry.Path == path))
        {
            return false;
        }

        consumed.Add(path);
        return true;
    }

    private static T? ReadOptionalObject<T>(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed, string path)
        where T : class
    {
        if (!IsListed(manifest, consumed, path))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(ShardPath(packageDirectory, path));
        return PackageValidator.ReadPayloadOrThrow<T>(bytes, path);
    }

    private static T ReadRequiredObject<T>(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed, string path)
        where T : class
    {
        return ReadOptionalObject<T>(packageDirectory, manifest, consumed, path)
            ?? throw new PublicationRejectedException("missing-artifact", path);
    }

    /// <summary>
    /// Reads one flat record-array family, merging every shard the manifest lists for it (its base key, or
    /// any <c>base.&lt;bucket&gt;.json</c> shard alongside it) back into a single array.
    /// </summary>
    private static ImmutableArray<T> ReadShardedArray<T>(
        string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed, string baseKey)
    {
        var shardPaths = ResolveShardPaths(manifest, baseKey);
        if (shardPaths.Length == 0)
        {
            return [];
        }

        var records = ImmutableArray.CreateBuilder<T>();
        foreach (var path in shardPaths)
        {
            consumed.Add(path);
            var bytes = File.ReadAllBytes(ShardPath(packageDirectory, path));
            records.AddRange(PackageValidator.ReadPayloadOrThrow<ImmutableArray<T>>(bytes, path));
        }

        return records.ToImmutable();
    }

    /// <summary>
    /// Reads every shard the manifest lists for one compound fact family (its base key, or any
    /// <c>base.&lt;bucket&gt;.json</c> shard alongside it -- GCPC-039, GCPC-041), deserialized as
    /// <typeparamref name="T"/>. Unlike a flat family's records, a compound family's shards are objects
    /// with several named arrays, so the caller merges each named array across shards itself.
    /// </summary>
    private static List<T> ReadCompoundShards<T>(
        string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed, string baseKey)
        where T : class
    {
        var shardPaths = ResolveShardPaths(manifest, baseKey);
        var shards = new List<T>(shardPaths.Length);
        foreach (var path in shardPaths)
        {
            consumed.Add(path);
            var bytes = File.ReadAllBytes(ShardPath(packageDirectory, path));
            shards.Add(PackageValidator.ReadPayloadOrThrow<T>(bytes, path));
        }

        return shards;
    }

    private static string[] ResolveShardPaths(ManifestEnvelope manifest, string baseKey)
    {
        var stem = baseKey.EndsWith(".json", StringComparison.Ordinal) ? baseKey[..^".json".Length] : baseKey;
        return manifest.Artifacts
            .Select(static entry => entry.Path)
            .Where(path => path == baseKey
                || (path.StartsWith(stem + ".", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal)))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static StructuralFactsShard? ReadStructural(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed)
    {
        var shards = ReadCompoundShards<StructuralFactsShard>(packageDirectory, manifest, consumed, "facts/structural.json");
        if (shards.Count == 0)
        {
            return null;
        }

        return new StructuralFactsShard(
            [.. shards.SelectMany(static shard => shard.Solutions)],
            [.. shards.SelectMany(static shard => shard.Projects)],
            [.. shards.SelectMany(static shard => shard.Documents)],
            [.. shards.SelectMany(static shard => shard.Symbols)]);
    }

    private static ArchitectureFactsShard? ReadArchitecture(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed)
    {
        var shards = ReadCompoundShards<ArchitectureFactsShard>(packageDirectory, manifest, consumed, "facts/architecture.json");
        if (shards.Count == 0)
        {
            return null;
        }

        return new ArchitectureFactsShard(
            [.. shards.SelectMany(static shard => shard.Components)],
            [.. shards.SelectMany(static shard => shard.DeploymentUnits)],
            [.. shards.SelectMany(static shard => shard.EntryPoints)],
            [.. shards.SelectMany(static shard => shard.BoundaryOperations)],
            [.. shards.SelectMany(static shard => shard.ExternalSystems)]);
    }

    private static ContractFactsShard? ReadContract(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed)
    {
        var shards = ReadCompoundShards<ContractFactsShard>(packageDirectory, manifest, consumed, "facts/contract.json");
        if (shards.Count == 0)
        {
            return null;
        }

        return new ContractFactsShard(
            [.. shards.SelectMany(static shard => shard.Contracts)],
            [.. shards.SelectMany(static shard => shard.ContractBindings)],
            [.. shards.SelectMany(static shard => shard.ContractRevisions)]);
    }

    private static PersistenceFactsShard? ReadPersistence(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed)
    {
        var shards = ReadCompoundShards<PersistenceFactsShard>(packageDirectory, manifest, consumed, "facts/persistence.json");
        if (shards.Count == 0)
        {
            return null;
        }

        return new PersistenceFactsShard(
            [.. shards.SelectMany(static shard => shard.DataStores)],
            [.. shards.SelectMany(static shard => shard.DataObjects)],
            [.. shards.SelectMany(static shard => shard.DataFields)],
            [.. shards.SelectMany(static shard => shard.DataOperations)]);
    }

    private static ConfigurationFactsShard? ReadConfiguration(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed)
    {
        var shards = ReadCompoundShards<ConfigurationFactsShard>(packageDirectory, manifest, consumed, "facts/configuration.json");
        if (shards.Count == 0)
        {
            return null;
        }

        return new ConfigurationFactsShard([.. shards.SelectMany(static shard => shard.ConfigurationBindings)]);
    }

    private static QuarantineEnvelope? ReadQuarantine(string packageDirectory, ManifestEnvelope manifest, HashSet<string> consumed)
    {
        var shards = ReadCompoundShards<QuarantineEnvelope>(packageDirectory, manifest, consumed, "quarantine/records.json");
        if (shards.Count == 0)
        {
            return null;
        }

        return new QuarantineEnvelope([.. shards.SelectMany(static shard => shard.Records)]);
    }

    private static string ManifestPath(string packageDirectory) =>
        ShardPath(packageDirectory, PackagePublisher.ManifestKey);

    private static string ShardPath(string packageDirectory, string relativeKey) =>
        Path.Combine(packageDirectory, relativeKey.Replace('/', Path.DirectorySeparatorChar));
}
