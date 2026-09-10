using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class PackagePublisher
{
    internal const string RegistryKey = "contracts/taxonomy-registry.json";
    internal const string ManifestKey = "manifest.json";

    /// <summary>
    /// Plans with an effectively unbounded ceiling, preserving the unsplit shape every caller of this
    /// overload already depends on. A caller that wants the derived ceiling actually enforced -- and a
    /// caller (such as <see cref="PublicationPipeline"/>) that already built a plan for citations, so the
    /// bytes written always agree with the citations minted against them (AD-023) -- should use the
    /// <see cref="LayoutPlan"/> overload instead.
    /// </summary>
    internal static ImmutableArray<StagedFragment> ToPublicationOrder(
        WireDocument document,
        ImmutableArray<StagedFragment> projections = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ToPublicationOrder(document, LayoutPlanner.Plan(document, int.MaxValue), projections);
    }

    internal static ImmutableArray<StagedFragment> ToPublicationOrder(
        WireDocument document,
        LayoutPlan plan,
        ImmutableArray<StagedFragment> projections = default,
        ProvenanceDto? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(plan);

        var view = PublishedPackageView.From(document, plan);
        var payloads = ImmutableArray.CreateBuilder<StagedFragment>(plan.Artifacts.Length);
        foreach (var artifact in plan.Artifacts)
        {
            var bytes = artifact.Records.IsEmpty
                ? Write(document, artifact.ArtifactKey)
                : LayoutPlanner.SerializeRecords(artifact.Records.Select(static record => record.Entry));
            payloads.Add(new StagedFragment(artifact.Role, artifact.ArtifactKey, bytes));
        }

        var fragments = payloads.ToImmutable();
        if (!projections.IsDefaultOrEmpty)
        {
            fragments = fragments.AddRange(projections);
        }

        var context = new ManifestContext(document.Manifest.SolutionKey, document.Manifest.SolutionFileName);
        var manifest = ManifestBuilder.From(context, fragments, view, provenance);
        return fragments.Add(new StagedFragment(ArtifactRole.Manifest, ManifestKey, CanonicalJson.Write(manifest)));
    }

    /// <summary>
    /// Assembles the whole-document, never-split artifacts: the taxonomy registry, the envelopes and the
    /// compound fact-family bundles. The flat record-array families (confirmed relations, candidates,
    /// unresolved records, open frontiers, observations) are no longer written here -- the plan already
    /// carries their exact, possibly-sharded byte content (see <see cref="ToPublicationOrder(WireDocument, LayoutPlan, ImmutableArray{StagedFragment})"/>).
    /// </summary>
    internal static ImmutableArray<byte> Write(WireDocument document, string canonicalKey)
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

        if (canonicalKey == "quarantine/records.json")
        {
            return CanonicalJson.Write(new QuarantineEnvelope(document.Quarantine));
        }

        throw new InvalidOperationException($"PublishedPackageView emitted unsupported canonical key '{canonicalKey}'.");
    }
}
