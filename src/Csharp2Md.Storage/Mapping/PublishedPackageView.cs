using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

public readonly record struct ArtifactSlot(string CanonicalKey, ArtifactRole Role, int Count);

public readonly record struct ArtifactCitation(string ArtifactKey, int Ordinal);

public sealed class PublishedPackageView
{
    private readonly ImmutableDictionary<string, ArtifactCitation> _factsById;

    public WireDocument Document { get; }

    public ImmutableArray<ArtifactSlot> Slots { get; }

    private PublishedPackageView(
        WireDocument document,
        ImmutableArray<ArtifactSlot> slots,
        ImmutableDictionary<string, ArtifactCitation> factsById)
    {
        Document = document;
        Slots = slots;
        _factsById = factsById;
    }

    public static PublishedPackageView From(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var slots = new List<ArtifactSlot>
        {
            new(PackagePublisher.RegistryKey, ArtifactRole.Payload, 1),
            new("coverage.json", ArtifactRole.Payload, 1),
            new("diagnostics.json", ArtifactRole.Payload, document.Diagnostics.Records.Length),
            new("measurements.json", ArtifactRole.Payload, document.Measurements.Records.Length),
            new("run-certification.json", ArtifactRole.Payload, 1),
        };

        AddIfPositive(
            slots,
            "facts/structural.json",
            document.Solutions.Length + document.Projects.Length
            + document.Documents.Length + document.Symbols.Length);
        AddIfPositive(
            slots,
            "facts/architecture.json",
            document.Components.Length + document.DeploymentUnits.Length
            + document.EntryPoints.Length + document.BoundaryOperations.Length
            + document.ExternalSystems.Length);
        AddIfPositive(
            slots,
            "facts/contract.json",
            document.Contracts.Length + document.ContractBindings.Length
            + document.ContractRevisions.Length);
        AddIfPositive(
            slots,
            "facts/persistence.json",
            document.DataStores.Length + document.DataObjects.Length
            + document.DataFields.Length + document.DataOperations.Length);
        AddIfPositive(slots, "facts/configuration.json", document.ConfigurationBindings.Length);

        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            if (!document.Observations.TryGetValue(kind.WireName, out var records) || records.IsDefaultOrEmpty)
            {
                continue;
            }

            slots.Add(new ArtifactSlot("observations/" + kind.WireName + ".json", ArtifactRole.Payload, records.Length));
        }

        foreach (var relation in TaxonomyTables.Default.Relations)
        {
            if (!document.ConfirmedRelations.TryGetValue(relation.WireName, out var records) || records.IsDefaultOrEmpty)
            {
                continue;
            }

            slots.Add(new ArtifactSlot(
                "relations/confirmed/" + relation.WireName + ".json",
                ArtifactRole.Payload,
                records.Length));
        }

        AddIfPositive(slots, "relations/candidates.json", document.Candidates.Length);
        AddIfPositive(slots, "relations/unresolved.json", document.Unresolved.Length);
        AddIfPositive(slots, "relations/frontiers.json", document.Frontiers.Length);
        AddIfPositive(slots, "quarantine/records.json", document.Quarantine.Length);

        slots.Sort(static (left, right) =>
            string.Compare(left.CanonicalKey, right.CanonicalKey, StringComparison.Ordinal));
        return new PublishedPackageView(document, [.. slots], IndexFacts(document));
    }

    public bool TryLocate(string factId, out ArtifactCitation citation)
    {
        if (string.IsNullOrEmpty(factId))
        {
            citation = default;
            return false;
        }

        return _factsById.TryGetValue(factId, out citation);
    }

    public bool TryLocateRelation(string kind, int index, out ArtifactCitation citation)
    {
        citation = default;
        if (string.IsNullOrEmpty(kind) || index < 0)
        {
            return false;
        }

        if (!Document.ConfirmedRelations.TryGetValue(kind, out var records)
            || records.IsDefaultOrEmpty
            || index >= records.Length)
        {
            return false;
        }

        citation = new ArtifactCitation("relations/confirmed/" + kind + ".json", index);
        return true;
    }

    private static ImmutableDictionary<string, ArtifactCitation> IndexFacts(WireDocument document)
    {
        var facts = ImmutableDictionary.CreateBuilder<string, ArtifactCitation>(StringComparer.Ordinal);
        Index(
            facts,
            "facts/structural.json",
            document.Solutions.Select(static dto => dto.Identity.Id),
            document.Projects.Select(static dto => dto.Identity.Id),
            document.Documents.Select(static dto => dto.Identity.Id),
            document.Symbols.Select(static dto => dto.Identity.Id));
        Index(
            facts,
            "facts/architecture.json",
            document.Components.Select(static dto => dto.Identity.Id),
            document.DeploymentUnits.Select(static dto => dto.Identity.Id),
            document.EntryPoints.Select(static dto => dto.Identity.Id),
            document.BoundaryOperations.Select(static dto => dto.Identity.Id),
            document.ExternalSystems.Select(static dto => dto.Identity.Id));
        Index(
            facts,
            "facts/contract.json",
            document.Contracts.Select(static dto => dto.Identity.Id),
            document.ContractBindings.Select(static dto => dto.Identity.Id),
            document.ContractRevisions.Select(static dto => dto.Identity.Id));
        Index(
            facts,
            "facts/persistence.json",
            document.DataStores.Select(static dto => dto.Identity.Id),
            document.DataObjects.Select(static dto => dto.Identity.Id),
            document.DataFields.Select(static dto => dto.Identity.Id),
            document.DataOperations.Select(static dto => dto.Identity.Id));
        Index(
            facts,
            "facts/configuration.json",
            document.ConfigurationBindings.Select(static dto => dto.Identity.Id));
        return facts.ToImmutable();
    }

    private static void Index(
        ImmutableDictionary<string, ArtifactCitation>.Builder facts,
        string artifactKey,
        params IEnumerable<string>[] sequences)
    {
        var ordinal = 0;
        foreach (var sequence in sequences)
        {
            foreach (var id in sequence)
            {
                facts[id] = new ArtifactCitation(artifactKey, ordinal++);
            }
        }
    }

    private static void AddIfPositive(List<ArtifactSlot> slots, string canonicalKey, int count)
    {
        if (count > 0)
        {
            slots.Add(new ArtifactSlot(canonicalKey, ArtifactRole.Payload, count));
        }
    }
}
