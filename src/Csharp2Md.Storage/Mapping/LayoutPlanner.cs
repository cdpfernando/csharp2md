using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

/// <summary>
/// One of the four mandatory coverage metrics (GCPC-002), used to route a layout-time degradation reason
/// (GCPC-004) onto the metric whose numerator the degraded family feeds.
/// </summary>
internal enum CoverageMetricKind
{
    EntryPoint,
    LinkedCall,
    Contract,
    Persistence,
}

/// <summary>One record's identity and its canonical JSON entry, placed at a specific ordinal inside its
/// planned artifact.</summary>
public sealed record PlannedRecord(string Identity, ImmutableArray<byte> Entry);

/// <summary>
/// One artifact this package will publish. <see cref="Records"/> is populated for every record-bearing
/// family this planner can shard -- the flat record-array families (confirmed relations, candidates,
/// unresolved records, open frontiers, observations) and the compound fact-family bundles alike -- so a
/// citation into any of them resolves to that one record's own bytes without re-deriving the whole
/// artifact (see <see cref="Csharp2Md.Storage.Validation.ProjectionValidator"/>). <see
/// cref="PrecomputedBytes"/> carries the exact bytes this artifact's file holds when that differs from
/// "each record's entry, concatenated into one JSON array" -- true for a compound fact-family bundle,
/// whose file is one JSON object with several named arrays, not a flat array. It is empty for the
/// fixed one-per-package envelope artifacts (manifest, registry, coverage, diagnostics, measurements,
/// run-certification), which <see cref="PackagePublisher"/> still assembles from the whole document.
/// </summary>
public sealed record PlannedArtifact(
    string ArtifactKey,
    ArtifactRole Role,
    int Count,
    ImmutableArray<PlannedRecord> Records,
    ImmutableArray<byte> PrecomputedBytes = default);

/// <summary>
/// Every artifact key and every record ordinal this package will publish, computed from the validated
/// wire document before any byte is written (AD-023). <see cref="PackagePublisher"/>, <see
/// cref="ManifestBuilder"/> and <see cref="PublishedPackageView"/> all consume this one plan.
/// </summary>
public sealed class LayoutPlan
{
    public ImmutableArray<PlannedArtifact> Artifacts { get; }

    /// <summary>Fact id to the artifact and ordinal that holds it -- including a fact published inside a
    /// (possibly sharded) compound fact-family bundle, e.g. <c>facts/structural.json</c>.</summary>
    public ImmutableDictionary<string, ArtifactCitation> FactLocations { get; }

    /// <summary>
    /// Confirmed-relation kind to the citation for each record at that kind's original position in
    /// <see cref="WireDocument.ConfirmedRelations"/> -- <c>RelationLocations["contains"][2]</c> is where
    /// the third <c>contains</c> record in the validated document ended up, however the family was split.
    /// </summary>
    public ImmutableDictionary<string, ImmutableArray<ArtifactCitation>> RelationLocations { get; }

    /// <summary>The citation for each record at its original position in <see cref="WireDocument.Candidates"/>,
    /// however the family was split (GCPC-041).</summary>
    public ImmutableArray<ArtifactCitation> CandidateLocations { get; }

    /// <summary>The citation for each record at its original position in <see cref="WireDocument.Unresolved"/>,
    /// however the family was split (GCPC-041).</summary>
    public ImmutableArray<ArtifactCitation> UnresolvedLocations { get; }

    /// <summary>The citation for each record at its original position in <see cref="WireDocument.Frontiers"/>,
    /// however the family was split (GCPC-041).</summary>
    public ImmutableArray<ArtifactCitation> FrontierLocations { get; }

    /// <summary>
    /// One entry per record that could not be reduced to fit the ceiling by further splitting -- it is
    /// published in a shard of its own rather than truncated (GCPC-038's edge case).
    /// </summary>
    public ImmutableArray<DegradationReasonDto> DegradationReasons { get; }

    /// <summary>
    /// The subset of <see cref="DegradationReasons"/> (GCPC-004) whose family feeds one of the four
    /// mandatory coverage metrics' numerator -- <c>invokes</c> confirmed relations feed
    /// <see cref="CoverageMetricKind.LinkedCall"/>, <c>accesses-data</c> feed
    /// <see cref="CoverageMetricKind.Persistence"/>, <c>uses-contract</c> feed
    /// <see cref="CoverageMetricKind.Contract"/>. <see cref="CoverageMetricKind.EntryPoint"/> has no
    /// routable source today: its numerator (<c>EntryPoint</c> facts) shares the
    /// <c>facts/architecture.json</c> compound family with unrelated fact types this planner does not
    /// distinguish by sub-type when recording a degradation, so an architecture-family degradation is
    /// never attributed to it rather than attributed on an unproven guess. A caller
    /// (<see cref="PublicationPipeline"/>) merges these onto the published <c>coverage.json</c> so a real
    /// degradation is never silently dropped from the metric it actually affects.
    /// </summary>
    internal ImmutableDictionary<CoverageMetricKind, ImmutableArray<DegradationReasonDto>> CoverageMetricDegradations { get; }

    public ImmutableArray<ArtifactSlot> Slots =>
        [.. Artifacts.Select(static artifact => new ArtifactSlot(artifact.ArtifactKey, artifact.Role, artifact.Count))];

    /// <summary>
    /// The per-artifact byte ceiling this plan was computed under (F6/GCPC-038): carried alongside the
    /// plan so a caller that shards something outside <see cref="Artifacts"/>' own family list -- the
    /// manifest itself, see <see cref="ManifestSharder"/> -- uses the exact same ceiling the rest of this
    /// publication was planned against, rather than re-deriving or hardcoding a second one.
    /// </summary>
    public int CeilingBytes { get; }

    internal LayoutPlan(
        ImmutableArray<PlannedArtifact> artifacts,
        ImmutableDictionary<string, ArtifactCitation> factLocations,
        ImmutableDictionary<string, ImmutableArray<ArtifactCitation>> relationLocations,
        ImmutableArray<ArtifactCitation> candidateLocations,
        ImmutableArray<ArtifactCitation> unresolvedLocations,
        ImmutableArray<ArtifactCitation> frontierLocations,
        ImmutableArray<DegradationReasonDto> degradationReasons,
        ImmutableDictionary<CoverageMetricKind, ImmutableArray<DegradationReasonDto>> coverageMetricDegradations,
        int ceilingBytes)
    {
        Artifacts = artifacts;
        FactLocations = factLocations;
        RelationLocations = relationLocations;
        CandidateLocations = candidateLocations;
        UnresolvedLocations = unresolvedLocations;
        FrontierLocations = frontierLocations;
        DegradationReasons = degradationReasons;
        CoverageMetricDegradations = coverageMetricDegradations;
        CeilingBytes = ceilingBytes;
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
        var metricDegradations = new Dictionary<CoverageMetricKind, ImmutableArray<DegradationReasonDto>.Builder>();
        var factLocations = ImmutableDictionary.CreateBuilder<string, ArtifactCitation>(StringComparer.Ordinal);

        // Envelope artifacts: always published, regardless of count, and never split. Each is a single
        // committed document (the taxonomy registry) or a singleton status/summary envelope whose own
        // size is bounded by the fixed set of metrics or reasons it carries, not by the population this
        // package describes -- unlike the fact and relation families below, splitting them would be
        // self-referential (a manifest of manifests) rather than a record-array shard.
        artifacts.Add(new PlannedArtifact(PackagePublisher.RegistryKey, ArtifactRole.Payload, 1, []));
        artifacts.Add(new PlannedArtifact("coverage.json", ArtifactRole.Payload, 1, []));
        artifacts.Add(new PlannedArtifact("diagnostics.json", ArtifactRole.Payload, document.Diagnostics.Records.Length, []));
        artifacts.Add(new PlannedArtifact("measurements.json", ArtifactRole.Payload, document.Measurements.Records.Length, []));
        artifacts.Add(new PlannedArtifact("run-certification.json", ArtifactRole.Payload, 1, []));

        PlanStructuralFamily(document, artifacts, factLocations, degradations, ceilingBytes);
        PlanArchitectureFamily(document, artifacts, factLocations, degradations, ceilingBytes);
        PlanContractFamily(document, artifacts, factLocations, degradations, ceilingBytes);
        PlanPersistenceFamily(document, artifacts, factLocations, degradations, ceilingBytes);
        PlanConfigurationFamily(document, artifacts, factLocations, degradations, ceilingBytes);

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

            if (!planDegradations.IsEmpty && CoverageMetricForRelation(relation.WireName) is { } metric)
            {
                AddMetricDegradations(metricDegradations, metric, planDegradations);
            }
        }

        var candidateLocations = PlanAndAdd(
            artifacts,
            degradations,
            "relations/candidates.json",
            document.Candidates.Select(static dto =>
                new RecordSource(RelationIdentity(dto.Kind, dto.Source.Id, dto.ProposedTarget.Id), CanonicalJson.Write(dto))),
            ceilingBytes);
        var unresolvedLocations = PlanAndAdd(
            artifacts,
            degradations,
            "relations/unresolved.json",
            document.Unresolved.Select(static dto => new RecordSource(dto.Source.Id, CanonicalJson.Write(dto))),
            ceilingBytes);
        var frontierLocations = PlanAndAdd(
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

        PlanQuarantineFamily(document, artifacts, degradations, ceilingBytes);

        var sorted = artifacts.ToImmutable().Sort(
            static (left, right) => string.CompareOrdinal(left.ArtifactKey, right.ArtifactKey));
        return new LayoutPlan(
            sorted,
            factLocations.ToImmutable(),
            relationLocations.ToImmutable(),
            candidateLocations,
            unresolvedLocations,
            frontierLocations,
            degradations.ToImmutable(),
            metricDegradations.ToImmutableDictionary(
                static pair => pair.Key, static pair => pair.Value.ToImmutable()),
            ceilingBytes);
    }

    /// <summary>Which coverage metric a confirmed-relation family's degradation reason affects (see
    /// <see cref="LayoutPlan.CoverageMetricDegradations"/>), or <see langword="null"/> when the relation
    /// kind feeds none of the four mandatory metrics.</summary>
    private static CoverageMetricKind? CoverageMetricForRelation(string wireName) => wireName switch
    {
        "invokes" => CoverageMetricKind.LinkedCall,
        "accesses-data" => CoverageMetricKind.Persistence,
        "uses-contract" => CoverageMetricKind.Contract,
        _ => null,
    };

    private static void AddMetricDegradations(
        Dictionary<CoverageMetricKind, ImmutableArray<DegradationReasonDto>.Builder> metricDegradations,
        CoverageMetricKind kind,
        ImmutableArray<DegradationReasonDto> reasons)
    {
        if (!metricDegradations.TryGetValue(kind, out var builder))
        {
            builder = ImmutableArray.CreateBuilder<DegradationReasonDto>();
            metricDegradations[kind] = builder;
        }

        builder.AddRange(reasons);
    }

    private readonly record struct RecordSource(string Identity, ImmutableArray<byte> Entry);

    private static ImmutableArray<ArtifactCitation> PlanAndAdd(
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        string baseKey,
        IEnumerable<RecordSource> sources,
        int ceilingBytes)
    {
        var (planned, citations, planDegradations) = PlanFamily(baseKey, sources, ceilingBytes);
        artifacts.AddRange(planned);
        degradations.AddRange(planDegradations);
        return citations;
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

        // Unsplit: keep the document's own order, exactly as the pre-plan writer did -- existing callers
        // (e.g. the unresolved/frontiers postings and catalogs) address these families by that natural
        // ordinal, not through a citation lookup. Only once a family is actually split does the physical
        // ordinal become an adaptive-sharding concern (below), which no caller has depended on until now.
        var inlineBytes = SerializeRecords(original.Select(static source => source.Entry)).Length;
        var citationByIndex = new ArtifactCitation[original.Length];
        if (inlineBytes <= ceilingBytes)
        {
            var records = original.Select(static source => new PlannedRecord(source.Identity, source.Entry)).ToImmutableArray();
            for (var ordinal = 0; ordinal < original.Length; ordinal++)
            {
                citationByIndex[ordinal] = new ArtifactCitation(baseKey, ordinal);
            }

            return (
                [new PlannedArtifact(baseKey, ArtifactRole.Payload, original.Length, records)],
                [.. citationByIndex],
                []);
        }

        var indices = Enumerable.Range(0, original.Length).ToArray();
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

    // --- Compound fact families -------------------------------------------------------------------
    //
    // A compound family (facts/structural.json and its four siblings, plus quarantine/records.json) is
    // one JSON object with several named arrays -- Solutions, Projects, Documents and Symbols for
    // "structural", for instance -- rather than one flat array. GCPC-039 still requires it to split once
    // its bytes exceed the ceiling, so the same adaptive SHA-256-prefix bucketing PlanFamily applies to a
    // flat family applies here too, keyed by each record's own identity regardless of which named array
    // it belongs to; the difference is only in how a candidate bucket's bytes get assembled (an object of
    // several typed sub-arrays, not one array of interchangeable entries) and how each individual
    // record's own bytes get produced (from its own typed DTO, for citation/value resolution -- see
    // PublishedPackageView and ProjectionValidator.AuthoritativeText).

    /// <summary>One record inside a compound family: which of the family's named arrays it belongs to
    /// (<see cref="GroupIndex"/>, e.g. 0 = Solutions, 1 = Projects for "structural"), its position in
    /// that array in the document (<see cref="GroupOrdinal"/>), and its fact id (<see cref="Identity"/>),
    /// used for bucket assignment exactly like a flat family's record identity.</summary>
    private readonly record struct CompoundRecord(int GroupIndex, int GroupOrdinal, string Identity);

    private static ImmutableArray<CompoundRecord> BuildCompoundRecords(params (int GroupIndex, IEnumerable<string> Ids)[] groups)
    {
        var list = ImmutableArray.CreateBuilder<CompoundRecord>();
        foreach (var (groupIndex, ids) in groups)
        {
            var ordinal = 0;
            foreach (var id in ids)
            {
                list.Add(new CompoundRecord(groupIndex, ordinal, id));
                ordinal++;
            }
        }

        return list.ToImmutable();
    }

    /// <summary>The subset of <paramref name="source"/> named by <paramref name="groupIndex"/> in
    /// <paramref name="subset"/>, in the document's own original order -- so a candidate shard's typed
    /// sub-array always reads the same as the unsplit family's, just restricted to that shard's members.</summary>
    private static ImmutableArray<T> ExtractGroup<T>(IReadOnlyList<CompoundRecord> subset, int groupIndex, ImmutableArray<T> source) =>
        [.. subset.Where(r => r.GroupIndex == groupIndex).OrderBy(r => r.GroupOrdinal).Select(r => source[r.GroupOrdinal])];

    private static CompoundRecord[] OrderCompound(IReadOnlyList<CompoundRecord> records) =>
        [.. records.OrderBy(static r => r.GroupIndex).ThenBy(static r => r.GroupOrdinal)];

    /// <summary>
    /// Plans one compound fact family: a single artifact at <paramref name="baseKey"/> when the whole
    /// bundle fits the ceiling (byte-identical to the pre-sharding shape every existing small fixture
    /// still produces), otherwise split into <c>baseKey.&lt;bucket&gt;.json</c> shards the same way
    /// <see cref="PlanFamily"/> splits a flat family (GCPC-039, GCPC-042, GCPC-043). <paramref
    /// name="assembleShard"/> builds the exact bytes for an arbitrary subset of records (used both to
    /// size-check a candidate bucket and to produce the final shard bytes); <paramref name="serializeOne"/>
    /// produces one record's own standalone bytes, so a citation into a shard still resolves to that
    /// single record without re-parsing the whole shard (mirrors a flat family's <see cref="PlannedRecord"/>).
    /// </summary>
    private static void PlanCompoundFamily(
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableDictionary<string, ArtifactCitation>.Builder factLocations,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        string baseKey,
        ImmutableArray<CompoundRecord> records,
        Func<IReadOnlyList<CompoundRecord>, ImmutableArray<byte>> assembleShard,
        Func<CompoundRecord, ImmutableArray<byte>> serializeOne,
        int ceilingBytes)
    {
        if (records.IsEmpty)
        {
            return;
        }

        var wholeBytes = assembleShard(records);
        if (wholeBytes.Length <= ceilingBytes)
        {
            var ordered = OrderCompound(records);
            artifacts.Add(new PlannedArtifact(
                baseKey, ArtifactRole.Payload, ordered.Length, BuildRecords(ordered, serializeOne), wholeBytes));
            AssignCitations(factLocations, baseKey, ordered);
            return;
        }

        var depthBytes = 1;
        Dictionary<string, List<CompoundRecord>> buckets;
        while (true)
        {
            buckets = new Dictionary<string, List<CompoundRecord>>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                var bucket = BucketKey(record.Identity, depthBytes);
                if (!buckets.TryGetValue(bucket, out var items))
                {
                    items = [];
                    buckets[bucket] = items;
                }

                items.Add(record);
            }

            var allFit = buckets.Values.All(items =>
                items.Count == 1 || assembleShard(OrderCompound(items)).Length <= ceilingBytes);
            if (allFit || depthBytes >= 32)
            {
                break;
            }

            depthBytes++;
        }

        foreach (var (bucket, items) in buckets.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var shardKey = ShardKey(baseKey, bucket);
            var ordered = OrderCompound(items);
            var shardBytes = assembleShard(ordered);
            artifacts.Add(new PlannedArtifact(
                shardKey, ArtifactRole.Payload, ordered.Length, BuildRecords(ordered, serializeOne), shardBytes));
            AssignCitations(factLocations, shardKey, ordered);

            if (ordered.Length == 1 && shardBytes.Length > ceilingBytes)
            {
                degradations.Add(new DegradationReasonDto(
                    "record-exceeds-ceiling",
                    $"Record '{ordered[0].Identity}' ({shardBytes.Length} bytes) exceeds the "
                    + $"{ceilingBytes}-byte ceiling in '{baseKey}' and is published in its own shard, never truncated.",
                    1));
            }
        }
    }

    private static ImmutableArray<PlannedRecord> BuildRecords(
        IReadOnlyList<CompoundRecord> ordered, Func<CompoundRecord, ImmutableArray<byte>> serializeOne) =>
        [.. ordered.Select(r => new PlannedRecord(r.Identity, serializeOne(r)))];

    private static void AssignCitations(
        ImmutableDictionary<string, ArtifactCitation>.Builder factLocations, string artifactKey, IReadOnlyList<CompoundRecord> ordered)
    {
        for (var ordinal = 0; ordinal < ordered.Count; ordinal++)
        {
            factLocations[ordered[ordinal].Identity] = new ArtifactCitation(artifactKey, ordinal);
        }
    }

    private static void PlanStructuralFamily(
        WireDocument document,
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableDictionary<string, ArtifactCitation>.Builder factLocations,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        int ceilingBytes)
    {
        var records = BuildCompoundRecords(
            (0, document.Solutions.Select(static dto => dto.Identity.Id)),
            (1, document.Projects.Select(static dto => dto.Identity.Id)),
            (2, document.Documents.Select(static dto => dto.Identity.Id)),
            (3, document.Symbols.Select(static dto => dto.Identity.Id)));

        PlanCompoundFamily(
            artifacts,
            factLocations,
            degradations,
            "facts/structural.json",
            records,
            subset => CanonicalJson.Write(new StructuralFactsShard(
                ExtractGroup(subset, 0, document.Solutions),
                ExtractGroup(subset, 1, document.Projects),
                ExtractGroup(subset, 2, document.Documents),
                ExtractGroup(subset, 3, document.Symbols))),
            record => record.GroupIndex switch
            {
                0 => CanonicalJson.Write(document.Solutions[record.GroupOrdinal]),
                1 => CanonicalJson.Write(document.Projects[record.GroupOrdinal]),
                2 => CanonicalJson.Write(document.Documents[record.GroupOrdinal]),
                _ => CanonicalJson.Write(document.Symbols[record.GroupOrdinal]),
            },
            ceilingBytes);
    }

    private static void PlanArchitectureFamily(
        WireDocument document,
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableDictionary<string, ArtifactCitation>.Builder factLocations,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        int ceilingBytes)
    {
        var records = BuildCompoundRecords(
            (0, document.Components.Select(static dto => dto.Identity.Id)),
            (1, document.DeploymentUnits.Select(static dto => dto.Identity.Id)),
            (2, document.EntryPoints.Select(static dto => dto.Identity.Id)),
            (3, document.BoundaryOperations.Select(static dto => dto.Identity.Id)),
            (4, document.ExternalSystems.Select(static dto => dto.Identity.Id)));

        PlanCompoundFamily(
            artifacts,
            factLocations,
            degradations,
            "facts/architecture.json",
            records,
            subset => CanonicalJson.Write(new ArchitectureFactsShard(
                ExtractGroup(subset, 0, document.Components),
                ExtractGroup(subset, 1, document.DeploymentUnits),
                ExtractGroup(subset, 2, document.EntryPoints),
                ExtractGroup(subset, 3, document.BoundaryOperations),
                ExtractGroup(subset, 4, document.ExternalSystems))),
            record => record.GroupIndex switch
            {
                0 => CanonicalJson.Write(document.Components[record.GroupOrdinal]),
                1 => CanonicalJson.Write(document.DeploymentUnits[record.GroupOrdinal]),
                2 => CanonicalJson.Write(document.EntryPoints[record.GroupOrdinal]),
                3 => CanonicalJson.Write(document.BoundaryOperations[record.GroupOrdinal]),
                _ => CanonicalJson.Write(document.ExternalSystems[record.GroupOrdinal]),
            },
            ceilingBytes);
    }

    private static void PlanContractFamily(
        WireDocument document,
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableDictionary<string, ArtifactCitation>.Builder factLocations,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        int ceilingBytes)
    {
        var records = BuildCompoundRecords(
            (0, document.Contracts.Select(static dto => dto.Identity.Id)),
            (1, document.ContractBindings.Select(static dto => dto.Identity.Id)),
            (2, document.ContractRevisions.Select(static dto => dto.Identity.Id)));

        PlanCompoundFamily(
            artifacts,
            factLocations,
            degradations,
            "facts/contract.json",
            records,
            subset => CanonicalJson.Write(new ContractFactsShard(
                ExtractGroup(subset, 0, document.Contracts),
                ExtractGroup(subset, 1, document.ContractBindings),
                ExtractGroup(subset, 2, document.ContractRevisions))),
            record => record.GroupIndex switch
            {
                0 => CanonicalJson.Write(document.Contracts[record.GroupOrdinal]),
                1 => CanonicalJson.Write(document.ContractBindings[record.GroupOrdinal]),
                _ => CanonicalJson.Write(document.ContractRevisions[record.GroupOrdinal]),
            },
            ceilingBytes);
    }

    private static void PlanPersistenceFamily(
        WireDocument document,
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableDictionary<string, ArtifactCitation>.Builder factLocations,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        int ceilingBytes)
    {
        var records = BuildCompoundRecords(
            (0, document.DataStores.Select(static dto => dto.Identity.Id)),
            (1, document.DataObjects.Select(static dto => dto.Identity.Id)),
            (2, document.DataFields.Select(static dto => dto.Identity.Id)),
            (3, document.DataOperations.Select(static dto => dto.Identity.Id)));

        PlanCompoundFamily(
            artifacts,
            factLocations,
            degradations,
            "facts/persistence.json",
            records,
            subset => CanonicalJson.Write(new PersistenceFactsShard(
                ExtractGroup(subset, 0, document.DataStores),
                ExtractGroup(subset, 1, document.DataObjects),
                ExtractGroup(subset, 2, document.DataFields),
                ExtractGroup(subset, 3, document.DataOperations))),
            record => record.GroupIndex switch
            {
                0 => CanonicalJson.Write(document.DataStores[record.GroupOrdinal]),
                1 => CanonicalJson.Write(document.DataObjects[record.GroupOrdinal]),
                2 => CanonicalJson.Write(document.DataFields[record.GroupOrdinal]),
                _ => CanonicalJson.Write(document.DataOperations[record.GroupOrdinal]),
            },
            ceilingBytes);
    }

    private static void PlanConfigurationFamily(
        WireDocument document,
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableDictionary<string, ArtifactCitation>.Builder factLocations,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        int ceilingBytes)
    {
        var records = BuildCompoundRecords(
            (0, document.ConfigurationBindings.Select(static dto => dto.Identity.Id)));

        PlanCompoundFamily(
            artifacts,
            factLocations,
            degradations,
            "facts/configuration.json",
            records,
            subset => CanonicalJson.Write(new ConfigurationFactsShard(
                ExtractGroup(subset, 0, document.ConfigurationBindings))),
            record => CanonicalJson.Write(document.ConfigurationBindings[record.GroupOrdinal]),
            ceilingBytes);
    }

    /// <summary>Quarantine records are keyed by <c>IdentityOrKey</c>, not a fact id -- they are never
    /// looked up through <see cref="LayoutPlan.FactLocations"/> (no caller has ever cited one that way),
    /// so its citations go to a scratch builder the plan discards.</summary>
    private static void PlanQuarantineFamily(
        WireDocument document,
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        ImmutableArray<DegradationReasonDto>.Builder degradations,
        int ceilingBytes)
    {
        var records = BuildCompoundRecords(
            (0, document.Quarantine.Select(static dto => dto.IdentityOrKey)));
        var scratchLocations = ImmutableDictionary.CreateBuilder<string, ArtifactCitation>(StringComparer.Ordinal);

        PlanCompoundFamily(
            artifacts,
            scratchLocations,
            degradations,
            "quarantine/records.json",
            records,
            subset => CanonicalJson.Write(new QuarantineEnvelope(ExtractGroup(subset, 0, document.Quarantine))),
            record => CanonicalJson.Write(document.Quarantine[record.GroupOrdinal]),
            ceilingBytes);
    }

    private static string RelationIdentity(string kind, string sourceId, string targetId) =>
        kind + ":" + sourceId + ":" + targetId;

    private static string ObservationIdentity(ObservationDto dto) =>
        $"{dto.Identity.Owner.Id}:{dto.Identity.Kind}:{dto.Identity.OccurrenceOrdinal}";
}
