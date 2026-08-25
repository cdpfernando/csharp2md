using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class PackagePublisher
{
    internal const string RegistryKey = "contracts/taxonomy-registry.json";
    internal const string ManifestKey = "manifest.json";

    internal static ImmutableArray<StagedFragment> ToPublicationOrder(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var payloads = new List<StagedFragment>
        {
            Payload(RegistryKey, document.TaxonomyRegistryCopy),
            Payload("coverage.json", CanonicalJson.Write(document.Coverage)),
            Payload("diagnostics.json", CanonicalJson.Write(document.Diagnostics)),
            Payload("measurements.json", CanonicalJson.Write(document.Measurements)),
            Payload("run-certification.json", CanonicalJson.Write(document.RunCertification)),
        };

        var structuralCount = document.Solutions.Length + document.Projects.Length
            + document.Documents.Length + document.Symbols.Length;
        if (structuralCount > 0)
        {
            payloads.Add(Payload(
                "facts/structural.json",
                CanonicalJson.Write(new StructuralFactsShard(
                    document.Solutions,
                    document.Projects,
                    document.Documents,
                    document.Symbols))));
        }

        var architectureCount = document.Components.Length + document.DeploymentUnits.Length
            + document.EntryPoints.Length + document.BoundaryOperations.Length + document.ExternalSystems.Length;
        if (architectureCount > 0)
        {
            payloads.Add(Payload(
                "facts/architecture.json",
                CanonicalJson.Write(new ArchitectureFactsShard(
                    document.Components,
                    document.DeploymentUnits,
                    document.EntryPoints,
                    document.BoundaryOperations,
                    document.ExternalSystems))));
        }

        var contractCount = document.Contracts.Length + document.ContractBindings.Length
            + document.ContractRevisions.Length;
        if (contractCount > 0)
        {
            payloads.Add(Payload(
                "facts/contract.json",
                CanonicalJson.Write(new ContractFactsShard(
                    document.Contracts,
                    document.ContractBindings,
                    document.ContractRevisions))));
        }

        var persistenceCount = document.DataStores.Length + document.DataObjects.Length
            + document.DataFields.Length + document.DataOperations.Length;
        if (persistenceCount > 0)
        {
            payloads.Add(Payload(
                "facts/persistence.json",
                CanonicalJson.Write(new PersistenceFactsShard(
                    document.DataStores,
                    document.DataObjects,
                    document.DataFields,
                    document.DataOperations))));
        }

        if (document.ConfigurationBindings.Length > 0)
        {
            payloads.Add(Payload(
                "facts/configuration.json",
                CanonicalJson.Write(new ConfigurationFactsShard(document.ConfigurationBindings))));
        }

        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            if (!document.Observations.TryGetValue(kind.WireName, out var records) || records.IsDefaultOrEmpty)
            {
                continue;
            }

            payloads.Add(Payload("observations/" + kind.WireName + ".json", CanonicalJson.Write(records)));
        }

        foreach (var relation in TaxonomyTables.Default.Relations)
        {
            if (!document.ConfirmedRelations.TryGetValue(relation.WireName, out var records) || records.IsDefaultOrEmpty)
            {
                continue;
            }

            payloads.Add(Payload(
                "relations/confirmed/" + relation.WireName + ".json",
                CanonicalJson.Write(records)));
        }

        if (!document.Candidates.IsDefaultOrEmpty)
        {
            payloads.Add(Payload("relations/candidates.json", CanonicalJson.Write(document.Candidates)));
        }

        if (!document.Unresolved.IsDefaultOrEmpty)
        {
            payloads.Add(Payload("relations/unresolved.json", CanonicalJson.Write(document.Unresolved)));
        }

        if (!document.Frontiers.IsDefaultOrEmpty)
        {
            payloads.Add(Payload("relations/frontiers.json", CanonicalJson.Write(document.Frontiers)));
        }

        if (!document.Quarantine.IsDefaultOrEmpty)
        {
            payloads.Add(Payload(
                "quarantine/records.json",
                CanonicalJson.Write(new QuarantineEnvelope(document.Quarantine))));
        }

        payloads.Sort(static (left, right) =>
            string.Compare(left.CanonicalKey, right.CanonicalKey, StringComparison.Ordinal));
        payloads.Add(new StagedFragment(ArtifactRole.Manifest, ManifestKey, CanonicalJson.Write(document.Manifest)));
        return [.. payloads];
    }

    private static StagedFragment Payload(string canonicalKey, ImmutableArray<byte> bytes) =>
        new(ArtifactRole.Payload, canonicalKey, bytes);
}
