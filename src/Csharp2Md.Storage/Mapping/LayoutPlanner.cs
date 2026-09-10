using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// One entry per record that could not be reduced to fit the ceiling by further splitting -- it is
    /// published in a shard of its own rather than truncated (GCPC-038's edge case).
    /// </summary>
    public ImmutableArray<DegradationReasonDto> DegradationReasons { get; }

    public ImmutableArray<ArtifactSlot> Slots =>
        [.. Artifacts.Select(static artifact => new ArtifactSlot(artifact.ArtifactKey, artifact.Role, artifact.Count))];

    internal LayoutPlan(
        ImmutableArray<PlannedArtifact> artifacts,
        ImmutableDictionary<string, ArtifactCitation> factLocations,
        ImmutableDictionary<string, ImmutableArray<ArtifactCitation>> relationLocations,
        ImmutableArray<DegradationReasonDto> degradationReasons)
    {
        Artifacts = artifacts;
        FactLocations = factLocations;
        RelationLocations = relationLocations;
        DegradationReasons = degradationReasons;
    }
}

/// <summary>
/// Computes the <see cref="LayoutPlan"/> for a validated wire document without writing or reading a
/// single byte of it (AD-023): every fact and relation gets exactly one planned location, and the plan
/// is a pure function of the document, so two runs over the same document always agree.
/// </summary>
public static class LayoutPlanner
{
    /// <summary>Plans using the derived default ceiling (<see cref="CeilingCalculator.Derive()"/>).</summary>
    public static LayoutPlan Plan(WireDocument document) => Plan(document, CeilingCalculator.Derive().CeilingBytes);

    public static LayoutPlan Plan(WireDocument document, int ceilingBytes)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ceilingBytes);

        var artifacts = ImmutableArray.CreateBuilder<PlannedArtifact>();
        var degradations = ImmutableArray.CreateBuilder<DegradationReasonDto>();

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
            var (planned, citations, planDegradations) = PlanFamily(baseKey, sources, ceilingBytes);
            artifacts.AddRange(planned);
            degradations.AddRange(planDegradations);
            relationLocations[relation.WireName] = citations;
        }

        PlanAndAdd(
            artifacts,
            degradations,
            "relations/candidates.json",
            document.Candidates.Select(static dto =>
                new RecordSource(RelationIdentity(dto.Kind, dto.Source.Id, dto.ProposedTarget.Id), CanonicalJson.Write(dto))),
            ceilingBytes);
        PlanAndAdd(
            artifacts,
            degradations,
            "relations/unresolved.json",
            document.Unresolved.Select(static dto => new RecordSource(dto.Source.Id, CanonicalJson.Write(dto))),
            ceilingBytes);
        PlanAndAdd(
            artifacts,
            degradations,
            "relations/frontiers.json",
            document.Frontiers.Select(static dto => new RecordSource(dto.Occurrence.Owner.Id, CanonicalJson.Write(dto))),
            ceilingBytes);

        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            if (!document.Observations.TryGetValue(kind.WireName, out var records) || records.IsDefaultOrEmpty)
            {
                continue;
            }

            PlanAndAdd(
                artifacts,
                degradations,
                "observations/" + kind.WireName + ".json",
                records.Select(static dto => new RecordSource(ObservationIdentity(dto), CanonicalJson.Write(dto))),
                ceilingBytes);
        }

        AddCompoundFamily(artifacts, "quarantine/records.json", document.Quarantine.Length);

        return new LayoutPlan(artifacts.ToImmutable(), factLocations, relationLocations.ToImmutable(), degradations.ToImmutable());
    }

    private readonly record struct RecordSource(string Identity, ImmutableArray<byte> Entry);

    private static void PlanAndAdd(
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        string baseKey,
        IEnumerable<RecordSource> sources,
        int ceilingBytes)
    {
        var (planned, _, planDegradations) = PlanFamily(baseKey, sources, ceilingBytes);
        artifacts.AddRange(planned);
        degradations.AddRange(planDegradations);
    }

    /// <summary>
    /// Plans one flat record-array family: a single artifact when its serialized bytes fit the ceiling,
    /// otherwise split by an adaptive SHA-256 prefix of each record's identity (never its display name --
    /// GCPC-043) that extends until every bucket fits, or until a bucket is irreducible (one record that
    /// alone still exceeds the ceiling, published in its own shard with a degradation reason rather than
    /// truncated -- GCPC-038's edge case). Bucketing depends only on identity and content, so two runs
    /// over the same document always agree (GCPC-042).
    /// </summary>
    private static (ImmutableArray<PlannedArtifact> Artifacts, ImmutableArray<ArtifactCitation> Citations, ImmutableArray<DegradationReasonDto> Degradations) PlanFamily(
        string baseKey,
        IEnumerable<RecordSource> sourceSequence,
        int ceilingBytes)
    {
        var original = sourceSequence as RecordSource[] ?? sourceSequence.ToArray();
        if (original.Length == 0)
        {
            return ([], [], []);
        }

        var indices = Enumerable.Range(0, original.Length).ToArray();
        Array.Sort(indices, (a, b) => string.CompareOrdinal(original[a].Identity, original[b].Identity));

        var inlineBytes = SerializeRecords(indices.Select(i => original[i].Entry)).Length;
        var citationByIndex = new ArtifactCitation[original.Length];
        if (inlineBytes <= ceilingBytes)
        {
            var records = indices.Select(i => new PlannedRecord(original[i].Identity, original[i].Entry)).ToImmutableArray();
            for (var ordinal = 0; ordinal < indices.Length; ordinal++)
            {
                citationByIndex[indices[ordinal]] = new ArtifactCitation(baseKey, ordinal);
            }

            return (
                [new PlannedArtifact(baseKey, ArtifactRole.Payload, original.Length, records)],
                [.. citationByIndex],
                []);
        }

        var depthBytes = 1;
        Dictionary<string, List<int>> buckets;
        while (true)
        {
            buckets = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (var index in indices)
            {
                var bucket = BucketKey(original[index].Identity, depthBytes);
                if (!buckets.TryGetValue(bucket, out var items))
                {
                    items = [];
                    buckets[bucket] = items;
                }

                items.Add(index);
            }

            var allFit = buckets.Values.All(items =>
                items.Count == 1 || SerializeRecords(items.Select(i => original[i].Entry)).Length <= ceilingBytes);
            if (allFit || depthBytes >= 32)
            {
                break;
            }

            depthBytes++;
        }

        var artifacts = ImmutableArray.CreateBuilder<PlannedArtifact>();
        var degradations = ImmutableArray.CreateBuilder<DegradationReasonDto>();
        foreach (var (bucket, items) in buckets.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var shardKey = ShardKey(baseKey, bucket);
            var sortedItems = items.OrderBy(i => original[i].Identity, StringComparer.Ordinal).ToArray();
            var shardRecords = sortedItems.Select(i => new PlannedRecord(original[i].Identity, original[i].Entry)).ToImmutableArray();
            artifacts.Add(new PlannedArtifact(shardKey, ArtifactRole.Payload, sortedItems.Length, shardRecords));
            for (var ordinal = 0; ordinal < sortedItems.Length; ordinal++)
            {
                citationByIndex[sortedItems[ordinal]] = new ArtifactCitation(shardKey, ordinal);
            }

            if (sortedItems.Length == 1)
            {
                var soleBytes = SerializeRecords([original[sortedItems[0]].Entry]).Length;
                if (soleBytes > ceilingBytes)
                {
                    degradations.Add(new DegradationReasonDto(
                        "record-exceeds-ceiling",
                        $"Record '{original[sortedItems[0]].Identity}' ({soleBytes} bytes) exceeds the "
                        + $"{ceilingBytes}-byte ceiling in '{baseKey}' and is published in its own shard, never truncated.",
                        1));
                }
            }
        }

        return (artifacts.ToImmutable(), [.. citationByIndex], degradations.ToImmutable());
    }

    /// <summary>The exact bytes <see cref="PackagePublisher"/> writes for a planned record-array
    /// artifact: its records, in the given order, as one canonical JSON array.</summary>
    internal static ImmutableArray<byte> SerializeRecords(IEnumerable<ImmutableArray<byte>> entries)
    {
        var array = new JsonArray();
        foreach (var entry in entries)
        {
            array.Add(JsonNode.Parse(entry.AsSpan()));
        }

        return CanonicalJson.Write((JsonNode)array);
    }

    private static string BucketKey(string identity, int depthBytes)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexStringLower(hash.AsSpan(0, depthBytes));
    }

    private static string ShardKey(string baseKey, string bucket)
    {
        var dot = baseKey.LastIndexOf('.');
        return dot < 0
            ? baseKey + "." + bucket
            : string.Concat(baseKey.AsSpan(0, dot), ".", bucket, baseKey.AsSpan(dot));
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
