using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage;

public static class FactualPackageReader
{
    private static readonly CoverageMetricDto ZeroCoverage = new(0, 0, 0, 0, []);

    public static PackageReadResult Read(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        if (!Directory.Exists(packageDirectory)
            || !File.Exists(ManifestPath(packageDirectory)))
        {
            throw new PublicationRejectedException("not-a-package", packageDirectory);
        }

        var report = PackageValidator.Validate(LoadDocument(packageDirectory));
        return new PackageReadResult(
            DomainMapper.FromWire(report.Document),
            MapQuarantine(report.Quarantine),
            report.Document.Coverage,
            report.Document.RunCertification);
    }

    private static WireDocument LoadDocument(string packageDirectory)
    {
        var structural = ReadOptionalObject<StructuralFactsShard>(packageDirectory, "facts/structural.json");
        var architecture = ReadOptionalObject<ArchitectureFactsShard>(packageDirectory, "facts/architecture.json");
        var contract = ReadOptionalObject<ContractFactsShard>(packageDirectory, "facts/contract.json");
        var persistence = ReadOptionalObject<PersistenceFactsShard>(packageDirectory, "facts/persistence.json");
        var configuration = ReadOptionalObject<ConfigurationFactsShard>(packageDirectory, "facts/configuration.json");

        var observations = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ObservationDto>>(StringComparer.Ordinal);
        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            var key = "observations/" + kind.WireName + ".json";
            var records = ReadOptionalArray<ObservationDto>(packageDirectory, key);
            if (!records.IsDefaultOrEmpty)
            {
                observations[kind.WireName] = records;
            }
        }

        var confirmed = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ConfirmedRelationDto>>(StringComparer.Ordinal);
        foreach (var relation in TaxonomyTables.Default.Relations)
        {
            var key = "relations/confirmed/" + relation.WireName + ".json";
            var records = ReadOptionalArray<ConfirmedRelationDto>(packageDirectory, key);
            if (!records.IsDefaultOrEmpty)
            {
                confirmed[relation.WireName] = records;
            }
        }

        var quarantineEnvelope = ReadOptionalObject<QuarantineEnvelope>(packageDirectory, "quarantine/records.json");
        var coverage = ReadOptionalObject<CoverageEnvelope>(packageDirectory, "coverage.json")
            ?? new CoverageEnvelope(ZeroCoverage, ZeroCoverage, ZeroCoverage, ZeroCoverage);
        var certification = ReadOptionalObject<RunCertificationEnvelope>(packageDirectory, "run-certification.json")
            ?? new RunCertificationEnvelope("not_evaluated");
        var diagnostics = ReadOptionalObject<DiagnosticsEnvelope>(packageDirectory, "diagnostics.json")
            ?? new DiagnosticsEnvelope([]);
        var measurements = ReadOptionalObject<MeasurementsEnvelope>(packageDirectory, "measurements.json")
            ?? new MeasurementsEnvelope([]);

        var manifestBytes = File.ReadAllBytes(ManifestPath(packageDirectory));
        var manifest = PackageValidator.ReadPayloadOrThrow<ManifestEnvelope>(manifestBytes, PackagePublisher.ManifestKey);

        var registryPath = ShardPath(packageDirectory, PackagePublisher.RegistryKey);
        var registry = File.Exists(registryPath)
            ? File.ReadAllBytes(registryPath).ToImmutableArray()
            : [];

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
            ReadOptionalArray<CandidateLinkDto>(packageDirectory, "relations/candidates.json"),
            ReadOptionalArray<UnresolvedRecordDto>(packageDirectory, "relations/unresolved.json"),
            ReadOptionalArray<OpenFrontierDto>(packageDirectory, "relations/frontiers.json"),
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

    private static T? ReadOptionalObject<T>(string packageDirectory, string relativeKey)
        where T : class
    {
        var path = ShardPath(packageDirectory, relativeKey);
        if (!File.Exists(path))
        {
            return null;
        }

        return PackageValidator.ReadPayloadOrThrow<T>(File.ReadAllBytes(path), relativeKey);
    }

    private static ImmutableArray<T> ReadOptionalArray<T>(string packageDirectory, string relativeKey)
    {
        var path = ShardPath(packageDirectory, relativeKey);
        if (!File.Exists(path))
        {
            return [];
        }

        return PackageValidator.ReadPayloadOrThrow<ImmutableArray<T>>(File.ReadAllBytes(path), relativeKey);
    }

    private static string ManifestPath(string packageDirectory) =>
        ShardPath(packageDirectory, PackagePublisher.ManifestKey);

    private static string ShardPath(string packageDirectory, string relativeKey) =>
        Path.Combine(packageDirectory, relativeKey.Replace('/', Path.DirectorySeparatorChar));
}
