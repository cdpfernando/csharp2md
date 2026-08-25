using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

public static class DomainMapper
{
    private static readonly CoverageMetricDto ZeroCoverage = new(0, 0, 0, 0, []);

    public static WireDocument ToWire(FactualSnapshot snapshot, ManifestContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        var versions = TaxonomyVersions.Initial;
        var artifacts = BuildEmptyManifestArtifacts();

        return new WireDocument(
            new ManifestEnvelope(
                versions.SchemaVersion,
                versions.TaxonomyVersion,
                versions.ObservationSchemaVersion,
                context.SolutionKey,
                context.SolutionFileName,
                artifacts),
            ReadEmbeddedRegistry(),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            ImmutableDictionary<string, ImmutableArray<ObservationDto>>.Empty,
            ImmutableDictionary<string, ImmutableArray<ConfirmedRelationDto>>.Empty,
            [],
            [],
            [],
            [],
            new CoverageEnvelope(ZeroCoverage, ZeroCoverage, ZeroCoverage, ZeroCoverage),
            new RunCertificationEnvelope("not_evaluated"),
            new DiagnosticsEnvelope([]),
            new MeasurementsEnvelope([]));
    }

    public static FactualSnapshot FromWire(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!IsEmptyPayload(document))
        {
            throw new NotSupportedException("Non-empty wire documents are mapped in a later task.");
        }

        return FactualSnapshot.Empty;
    }

    private static ImmutableArray<ManifestEntry> BuildEmptyManifestArtifacts()
    {
        var artifacts = ImmutableArray.CreateBuilder<ManifestEntry>();

        foreach (var family in Enum.GetValues<FactFamily>())
        {
            var segment = family.ToString().ToLowerInvariant();
            artifacts.Add(new ManifestEntry(
                "facts/" + segment,
                "payload",
                0,
                "facts/" + segment + ".json"));
        }

        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            artifacts.Add(new ManifestEntry(
                "observations/" + kind.WireName,
                "payload",
                0,
                "observations/" + kind.WireName + ".json"));
        }

        foreach (var relation in TaxonomyTables.Default.Relations)
        {
            artifacts.Add(new ManifestEntry(
                "relations/confirmed/" + relation.WireName,
                "payload",
                0,
                "relations/confirmed/" + relation.WireName + ".json"));
        }

        artifacts.Add(new ManifestEntry("relations/candidates", "payload", 0, "relations/candidates.json"));
        artifacts.Add(new ManifestEntry("relations/unresolved", "payload", 0, "relations/unresolved.json"));
        artifacts.Add(new ManifestEntry("relations/frontiers", "payload", 0, "relations/frontiers.json"));

        return artifacts.ToImmutable();
    }

    private static bool IsEmptyPayload(WireDocument document) =>
        document.Solutions.IsEmpty
        && document.Projects.IsEmpty
        && document.Documents.IsEmpty
        && document.Symbols.IsEmpty
        && document.Components.IsEmpty
        && document.DeploymentUnits.IsEmpty
        && document.EntryPoints.IsEmpty
        && document.BoundaryOperations.IsEmpty
        && document.ExternalSystems.IsEmpty
        && document.Contracts.IsEmpty
        && document.ContractBindings.IsEmpty
        && document.ContractRevisions.IsEmpty
        && document.DataStores.IsEmpty
        && document.DataObjects.IsEmpty
        && document.DataFields.IsEmpty
        && document.DataOperations.IsEmpty
        && document.ConfigurationBindings.IsEmpty
        && document.Observations.IsEmpty
        && document.ConfirmedRelations.IsEmpty
        && document.Candidates.IsEmpty
        && document.Unresolved.IsEmpty
        && document.Frontiers.IsEmpty;

    private static ImmutableArray<byte> ReadEmbeddedRegistry()
    {
        var assembly = typeof(DomainMapper).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("taxonomy-registry.json", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray().ToImmutableArray();
    }
}
