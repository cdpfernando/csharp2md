using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

/// <summary>One record's identity and its canonical JSON entry, placed at a specific ordinal inside its
/// planned artifact.</summary>
public sealed record PlannedRecord(string Identity, ImmutableArray<byte> Entry);

/// <summary>
/// One artifact this package will publish. <see cref="Records"/> is populated only for the flat
/// record-array families this planner can shard (confirmed relations, candidates, unresolved records,
/// open frontiers, observations); it is empty for the compound fact-family and envelope artifacts that
/// <see cref="PackagePublisher"/> still assembles from the whole document.
/// </summary>
public sealed record PlannedArtifact(string ArtifactKey, ArtifactRole Role, int Count, ImmutableArray<PlannedRecord> Records);

/// <summary>
/// Every artifact key and every record ordinal this package will publish, computed from the validated
/// wire document before any byte is written (AD-023). <see cref="PackagePublisher"/>, <see
/// cref="ManifestBuilder"/> and <see cref="PublishedPackageView"/> all consume this one plan.
/// </summary>
public sealed class LayoutPlan
{
    public ImmutableArray<PlannedArtifact> Artifacts { get; }

    /// <summary>Fact id to the artifact and ordinal that holds it.</summary>
    public ImmutableDictionary<string, ArtifactCitation> FactLocations { get; }

    /// <summary>
    /// Confirmed-relation kind to the citation for each record at that kind's original position in
    /// <see cref="WireDocument.ConfirmedRelations"/> -- <c>RelationLocations["contains"][2]</c> is where
    /// the third <c>contains</c> record in the validated document ended up, however the family was split.
    /// </summary>
    public ImmutableDictionary<string, ImmutableArray<ArtifactCitation>> RelationLocations { get; }

    public ImmutableArray<ArtifactSlot> Slots =>
        [.. Artifacts.Select(static artifact => new ArtifactSlot(artifact.ArtifactKey, artifact.Role, artifact.Count))];

    internal LayoutPlan(
        ImmutableArray<PlannedArtifact> artifacts,
        ImmutableDictionary<string, ArtifactCitation> factLocations,
        ImmutableDictionary<string, ImmutableArray<ArtifactCitation>> relationLocations)
    {
        Artifacts = artifacts;
        FactLocations = factLocations;
        RelationLocations = relationLocations;
    }
}

/// <summary>
/// Computes the <see cref="LayoutPlan"/> for a validated wire document without writing or reading a
/// single byte of it (AD-023): every fact and relation gets exactly one planned location, and the plan
/// is a pure function of the document, so two runs over the same document always agree.
/// </summary>
public static class LayoutPlanner
{
    public static LayoutPlan Plan(WireDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var artifacts = ImmutableArray.CreateBuilder<PlannedArtifact>();

        AddCompoundFamily(
            artifacts,
            "facts/structural.json",
            document.Solutions.Length + document.Projects.Length
            + document.Documents.Length + document.Symbols.Length);
        AddCompoundFamily(
            artifacts,
            "facts/architecture.json",
            document.Components.Length + document.DeploymentUnits.Length
            + document.EntryPoints.Length + document.BoundaryOperations.Length
            + document.ExternalSystems.Length);
        AddCompoundFamily(
            artifacts,
            "facts/contract.json",
            document.Contracts.Length + document.ContractBindings.Length
            + document.ContractRevisions.Length);
        AddCompoundFamily(
            artifacts,
            "facts/persistence.json",
            document.DataStores.Length + document.DataObjects.Length
            + document.DataFields.Length + document.DataOperations.Length);
        AddCompoundFamily(artifacts, "facts/configuration.json", document.ConfigurationBindings.Length);

        var factLocations = IndexFacts(document);

        var relationLocations = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ArtifactCitation>>(StringComparer.Ordinal);
        foreach (var relation in TaxonomyTables.Default.Relations)
        {
            if (!document.ConfirmedRelations.TryGetValue(relation.WireName, out var records) || records.IsDefaultOrEmpty)
            {
                continue;
            }

            var baseKey = "relations/confirmed/" + relation.WireName + ".json";
            var sources = records.Select(static dto =>
                new RecordSource(RelationIdentity(dto.Kind, dto.Source.Id, dto.Target.Id), CanonicalJson.Write(dto)));
            var (planned, citations) = PlanFamily(baseKey, sources);
            artifacts.AddRange(planned);
            relationLocations[relation.WireName] = citations;
        }

        PlanAndAdd(
            artifacts,
            "relations/candidates.json",
            document.Candidates.Select(static dto =>
                new RecordSource(RelationIdentity(dto.Kind, dto.Source.Id, dto.ProposedTarget.Id), CanonicalJson.Write(dto))));
        PlanAndAdd(
            artifacts,
            "relations/unresolved.json",
            document.Unresolved.Select(static dto => new RecordSource(dto.Source.Id, CanonicalJson.Write(dto))));
        PlanAndAdd(
            artifacts,
            "relations/frontiers.json",
            document.Frontiers.Select(static dto => new RecordSource(dto.Occurrence.Owner.Id, CanonicalJson.Write(dto))));

        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            if (!document.Observations.TryGetValue(kind.WireName, out var records) || records.IsDefaultOrEmpty)
            {
                continue;
            }

            PlanAndAdd(
                artifacts,
                "observations/" + kind.WireName + ".json",
                records.Select(static dto => new RecordSource(ObservationIdentity(dto), CanonicalJson.Write(dto))));
        }

        AddCompoundFamily(artifacts, "quarantine/records.json", document.Quarantine.Length);

        return new LayoutPlan(artifacts.ToImmutable(), factLocations, relationLocations.ToImmutable());
    }

    private readonly record struct RecordSource(string Identity, ImmutableArray<byte> Entry);

    private static void PlanAndAdd(
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        string baseKey,
        IEnumerable<RecordSource> sources)
    {
        var (planned, _) = PlanFamily(baseKey, sources);
        artifacts.AddRange(planned);
    }

    /// <summary>
    /// Plans one flat record-array family as a single, unsplit artifact. T35 extends this to split the
    /// family across shards once its serialized size exceeds the derived ceiling.
    /// </summary>
    private static (ImmutableArray<PlannedArtifact> Artifacts, ImmutableArray<ArtifactCitation> Citations) PlanFamily(
        string baseKey,
        IEnumerable<RecordSource> sources)
    {
        var ordered = sources.ToImmutableArray();
        if (ordered.IsEmpty)
        {
            return ([], []);
        }

        var records = ordered.Select(static source => new PlannedRecord(source.Identity, source.Entry)).ToImmutableArray();
        var citations = ordered.Select((_, index) => new ArtifactCitation(baseKey, index)).ToImmutableArray();
        return ([new PlannedArtifact(baseKey, ArtifactRole.Payload, ordered.Length, records)], citations);
    }

    private static void AddCompoundFamily(ImmutableArray<PlannedArtifact>.Builder artifacts, string artifactKey, int count)
    {
        if (count > 0)
        {
            artifacts.Add(new PlannedArtifact(artifactKey, ArtifactRole.Payload, count, []));
        }
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

    private static string RelationIdentity(string kind, string sourceId, string targetId) =>
        kind + ":" + sourceId + ":" + targetId;

    private static string ObservationIdentity(ObservationDto dto) =>
        $"{dto.Identity.Owner.Id}:{dto.Identity.Kind}:{dto.Identity.OccurrenceOrdinal}";
}
