using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class PackagePublisher
{
    internal const string RegistryKey = "contracts/taxonomy-registry.json";
    internal const string ManifestKey = "manifest.json";

    internal static ImmutableArray<StagedFragment> ToPublicationOrder(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var view = PublishedPackageView.From(document);
        var payloads = ImmutableArray.CreateBuilder<StagedFragment>(view.Slots.Length);
        foreach (var slot in view.Slots)
        {
            payloads.Add(new StagedFragment(slot.Role, slot.CanonicalKey, Write(document, slot.CanonicalKey)));
        }

        var fragments = payloads.ToImmutable();
        var context = new ManifestContext(document.Manifest.SolutionKey, document.Manifest.SolutionFileName);
        var manifest = ManifestBuilder.From(context, fragments, view);
        return fragments.Add(new StagedFragment(ArtifactRole.Manifest, ManifestKey, CanonicalJson.Write(manifest)));
    }

    private static ImmutableArray<byte> Write(WireDocument document, string canonicalKey)
    {
        if (canonicalKey == RegistryKey)
        {
            return document.TaxonomyRegistryCopy;
        }

        if (canonicalKey == "coverage.json")
        {
            return CanonicalJson.Write(document.Coverage);
        }

        if (canonicalKey == "diagnostics.json")
        {
            return CanonicalJson.Write(document.Diagnostics);
        }

        if (canonicalKey == "measurements.json")
        {
            return CanonicalJson.Write(document.Measurements);
        }

        if (canonicalKey == "run-certification.json")
        {
            return CanonicalJson.Write(document.RunCertification);
        }

        if (canonicalKey == "facts/structural.json")
        {
            return CanonicalJson.Write(new StructuralFactsShard(
                document.Solutions,
                document.Projects,
                document.Documents,
                document.Symbols));
        }

        if (canonicalKey == "facts/architecture.json")
        {
            return CanonicalJson.Write(new ArchitectureFactsShard(
                document.Components,
                document.DeploymentUnits,
                document.EntryPoints,
                document.BoundaryOperations,
                document.ExternalSystems));
        }

        if (canonicalKey == "facts/contract.json")
        {
            return CanonicalJson.Write(new ContractFactsShard(
                document.Contracts,
                document.ContractBindings,
                document.ContractRevisions));
        }

        if (canonicalKey == "facts/persistence.json")
        {
            return CanonicalJson.Write(new PersistenceFactsShard(
                document.DataStores,
                document.DataObjects,
                document.DataFields,
                document.DataOperations));
        }

        if (canonicalKey == "facts/configuration.json")
        {
            return CanonicalJson.Write(new ConfigurationFactsShard(document.ConfigurationBindings));
        }

        if (canonicalKey == "relations/candidates.json")
        {
            return CanonicalJson.Write(document.Candidates);
        }

        if (canonicalKey == "relations/unresolved.json")
        {
            return CanonicalJson.Write(document.Unresolved);
        }

        if (canonicalKey == "relations/frontiers.json")
        {
            return CanonicalJson.Write(document.Frontiers);
        }

        if (canonicalKey == "quarantine/records.json")
        {
            return CanonicalJson.Write(new QuarantineEnvelope(document.Quarantine));
        }

        const string observationPrefix = "observations/";
        if (TryFamilyName(canonicalKey, observationPrefix, out var observationKind))
        {
            return CanonicalJson.Write(document.Observations[observationKind]);
        }

        const string confirmedPrefix = "relations/confirmed/";
        if (TryFamilyName(canonicalKey, confirmedPrefix, out var relationKind))
        {
            return CanonicalJson.Write(document.ConfirmedRelations[relationKind]);
        }

        throw new InvalidOperationException($"PublishedPackageView emitted unsupported canonical key '{canonicalKey}'.");
    }

    private static bool TryFamilyName(string canonicalKey, string prefix, out string name)
    {
        if (canonicalKey.StartsWith(prefix, StringComparison.Ordinal)
            && canonicalKey.EndsWith(".json", StringComparison.Ordinal)
            && canonicalKey.Length > prefix.Length + ".json".Length)
        {
            name = canonicalKey[prefix.Length..^".json".Length];
            return name.Length > 0;
        }

        name = string.Empty;
        return false;
    }
}
