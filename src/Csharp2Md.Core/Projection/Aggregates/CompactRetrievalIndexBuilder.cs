namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed class CompactRetrievalIndexBuilder
{
    private static readonly string[] ResolutionNames =
        ["exact", "partial", "syntactic", "heuristic", "candidate", "unresolved", "not-applicable"];

    private static readonly string[] ResolutionMethodNames =
        ["exact", "candidate", "syntactic", "configured", "convention", "dynamic", "heuristic", "unresolved"];

    private readonly SortedDictionary<string, CompactDocumentMetadata> _knownDocuments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CompactDocumentMetadata> _evidenceOnlyDocuments = new(StringComparer.Ordinal);
    private readonly SortedSet<CompactOriginMetadata> _origins = new(OriginComparer.Instance);
    private readonly List<CompactRelationObservation> _relations = [];
    private readonly HashSet<int> _ordinals = [];

    public void AddKnownDocument(CompactDocumentMetadata document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateDocument(document);

        if (_knownDocuments.TryGetValue(document.DocumentId, out var existing) && existing != document)
        {
            throw new InvalidOperationException(
                $"Conflicting metadata for document '{document.DocumentId}': " +
                $"existing project/path/generated '{existing.ProjectId ?? "<null>"}/{existing.RelativePath}/{existing.GeneratedOrigin}', " +
                $"received '{document.ProjectId ?? "<null>"}/{document.RelativePath}/{document.GeneratedOrigin}'.");
        }

        if (_evidenceOnlyDocuments.TryGetValue(document.DocumentId, out var evidenceOnly) &&
            !SharedMetadataMatches(document, evidenceOnly))
        {
            throw new InvalidOperationException($"Conflicting evidence metadata for document '{document.DocumentId}'.");
        }

        _knownDocuments[document.DocumentId] = document;
    }

    public void AddRelation(CompactRelationObservation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);
        ValidateRelation(relation);
        _origins.Add(relation.Origin);
        foreach (var evidence in relation.Evidence)
        {
            AddEvidenceDocument(evidence);
        }

        _relations.Add(relation);
    }

    public CompactRetrievalIndexBuildResult Build()
    {
        var relations = _relations.OrderBy(static relation => relation.Ordinal).ToImmutableArray();
        var documents = BuildDocuments(relations);
        var postings = BuildPostings(relations);
        var entryPoints = ProvenEntryPoints(relations);
        var impact = EndpointImpact(relations);
        var unknownGroups = BuildUnknownGroups(relations, entryPoints, impact);
        var metrics = BuildMetrics(relations, entryPoints.Count);

        return new CompactRetrievalIndexBuildResult(
            documents,
            _origins.ToImmutableArray(),
            postings,
            unknownGroups,
            metrics);
    }

    private ImmutableArray<CompactDocumentMetadata> BuildDocuments(ImmutableArray<CompactRelationObservation> relations)
    {
        var evidenceOnlyOrder = relations.SelectMany(static relation => relation.Evidence)
            .Select(static evidence => evidence.DocumentId)
            .Where(id => !_knownDocuments.ContainsKey(id))
            .Distinct(StringComparer.Ordinal);
        return _knownDocuments.Values
            .Concat(evidenceOnlyOrder.Select(id => _evidenceOnlyDocuments[id]))
            .ToImmutableArray();
    }

    private CompactPostingMaps BuildPostings(ImmutableArray<CompactRelationObservation> relations)
    {
        var projects = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var sources = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var targets = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var kinds = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        var resolutions = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var relation in relations)
        {
            var projectId = relation.Evidence
                .Select(evidence => _knownDocuments.GetValueOrDefault(evidence.DocumentId)?.ProjectId)
                .FirstOrDefault(static project => project is not null);
            if (projectId is not null)
            {
                AddPosting(projects, projectId, relation.Ordinal);
            }

            AddPosting(sources, relation.SourceId, relation.Ordinal);
            if (relation.TargetId is not null)
            {
                AddPosting(targets, relation.TargetId, relation.Ordinal);
            }

            AddPosting(kinds, relation.RelationKind, relation.Ordinal);
            AddPosting(resolutions, relation.Resolution, relation.Ordinal);
        }

        return new CompactPostingMaps(
            PostingLists(projects),
            PostingLists(sources),
            PostingLists(targets),
            PostingLists(kinds),
            PostingLists(resolutions));
    }

    private static HashSet<string> ProvenEntryPoints(ImmutableArray<CompactRelationObservation> relations) =>
        relations
            .Where(static relation => relation.RelationKind == "aspnet-entrypoint" && relation.Resolution == "exact")
            .Select(static relation => relation.SourceId)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, int> EndpointImpact(ImmutableArray<CompactRelationObservation> relations) =>
        relations
            .SelectMany(static relation => relation.TargetId is null
                ? [relation.SourceId]
                : new[] { relation.SourceId, relation.TargetId })
            .GroupBy(static endpoint => endpoint, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

    private static ImmutableArray<CompactUnknownGroup> BuildUnknownGroups(
        ImmutableArray<CompactRelationObservation> relations,
        HashSet<string> entryPoints,
        IReadOnlyDictionary<string, int> impact) =>
        relations
            .Where(static relation => relation.Resolution == "unresolved")
            .GroupBy(static relation => new UnknownKey(
                relation.UnresolvedReason ?? string.Empty,
                relation.SourceId,
                relation.ObservedTargetText ?? string.Empty))
            .Select(group => new CompactUnknownGroup(
                group.Key.UnresolvedReason,
                group.Key.SourceId,
                group.Key.ObservedTargetText,
                group.Count(),
                entryPoints.Contains(group.Key.SourceId),
                impact[group.Key.SourceId],
                group.Select(static relation => relation.Ordinal).Distinct().Order().ToImmutableArray()))
            .OrderByDescending(static group => group.HasProvenEntryPoint)
            .ThenByDescending(static group => group.Impact)
            .ThenBy(static group => group.RelationOrdinals[0])
            .ToImmutableArray();

    private static CompactSummaryMetrics BuildMetrics(
        ImmutableArray<CompactRelationObservation> relations,
        int mappedEntryPointCount) =>
        new(
            relations.SelectMany(static relation => relation.TargetId is null
                    ? [relation.SourceId]
                    : new[] { relation.SourceId, relation.TargetId })
                .Distinct(StringComparer.Ordinal).Count(),
            mappedEntryPointCount,
            Metrics(relations, static relation => relation.Partition),
            Metrics(relations, static relation => relation.RelationKind));

    private void ValidateRelation(CompactRelationObservation relation)
    {
        if (relation.Ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relation), relation.Ordinal, "Relation ordinal must be non-negative.");
        }

        if (!_ordinals.Add(relation.Ordinal))
        {
            throw new InvalidOperationException($"Duplicate relation ordinal '{relation.Ordinal}'.");
        }

        Require(relation.SourceId, nameof(relation.SourceId));
        if (relation.TargetId is not null)
        {
            Require(relation.TargetId, nameof(relation.TargetId));
        }

        Require(relation.Partition, nameof(relation.Partition));
        Require(relation.RelationKind, nameof(relation.RelationKind));
        RequireKnownValue(relation.Resolution, ResolutionNames, nameof(relation.Resolution));
        RequireKnownValue(relation.ResolutionMethod, ResolutionMethodNames, nameof(relation.ResolutionMethod));
        ArgumentNullException.ThrowIfNull(relation.Origin);
        Require(relation.Origin.FragmentReference, nameof(relation.Origin.FragmentReference));
        Require(relation.Origin.FragmentSha256, nameof(relation.Origin.FragmentSha256));
        Require(relation.Origin.GeneratorVersion, nameof(relation.Origin.GeneratorVersion));
    }

    private void AddEvidenceDocument(CompactDocumentMetadata evidence)
    {
        ValidateDocument(evidence);
        if (_knownDocuments.TryGetValue(evidence.DocumentId, out var known))
        {
            if (!SharedMetadataMatches(known, evidence))
            {
                throw new InvalidOperationException($"Conflicting evidence metadata for document '{evidence.DocumentId}'.");
            }

            return;
        }

        if (_evidenceOnlyDocuments.TryGetValue(evidence.DocumentId, out var existing) && existing != evidence)
        {
            throw new InvalidOperationException($"Conflicting evidence metadata for document '{evidence.DocumentId}'.");
        }

        _evidenceOnlyDocuments.TryAdd(evidence.DocumentId, evidence);
    }

    private static ImmutableArray<CompactQualityMetric> Metrics(
        ImmutableArray<CompactRelationObservation> relations,
        Func<CompactRelationObservation, string> keySelector) =>
        relations.GroupBy(keySelector, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group => new CompactQualityMetric(
                group.Key,
                group.Count(),
                Counts(group.Select(static relation => relation.Resolution), ResolutionNames),
                Counts(group.Select(static relation => relation.ResolutionMethod), ResolutionMethodNames)))
            .ToImmutableArray();

    private static ImmutableArray<CompactNamedCount> Counts(IEnumerable<string> values, string[] names)
    {
        var counts = values.GroupBy(static value => value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        return names.Select(name => new CompactNamedCount(name, counts.GetValueOrDefault(name))).ToImmutableArray();
    }

    private static ImmutableArray<CompactPostingList> PostingLists(SortedDictionary<string, List<int>> postings) =>
        postings.Select(static pair => new CompactPostingList(pair.Key, pair.Value.Distinct().Order().ToImmutableArray()))
            .ToImmutableArray();

    private static void AddPosting(SortedDictionary<string, List<int>> postings, string key, int ordinal)
    {
        if (!postings.TryGetValue(key, out var ordinals))
        {
            postings.Add(key, ordinals = []);
        }

        ordinals.Add(ordinal);
    }

    private static void ValidateDocument(CompactDocumentMetadata document)
    {
        Require(document.DocumentId, nameof(document.DocumentId));
        Require(document.RelativePath, nameof(document.RelativePath));
    }

    private static bool SharedMetadataMatches(CompactDocumentMetadata left, CompactDocumentMetadata right) =>
        StringComparer.Ordinal.Equals(left.RelativePath, right.RelativePath) &&
        left.GeneratedOrigin == right.GeneratedOrigin &&
        (right.ProjectId is null || StringComparer.Ordinal.Equals(left.ProjectId, right.ProjectId));

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{name}' cannot be empty.", name);
        }
    }

    private static void RequireKnownValue(string value, string[] knownValues, string name)
    {
        Require(value, name);
        if (!knownValues.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(name, value, $"Unknown {name} value.");
        }
    }

    private sealed class OriginComparer : IComparer<CompactOriginMetadata>
    {
        public static OriginComparer Instance { get; } = new();

        public int Compare(CompactOriginMetadata? x, CompactOriginMetadata? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var reference = StringComparer.Ordinal.Compare(x.FragmentReference, y.FragmentReference);
            if (reference != 0) return reference;
            var hash = StringComparer.Ordinal.Compare(x.FragmentSha256, y.FragmentSha256);
            return hash != 0 ? hash : StringComparer.Ordinal.Compare(x.GeneratorVersion, y.GeneratorVersion);
        }
    }

    private readonly record struct UnknownKey(string UnresolvedReason, string SourceId, string ObservedTargetText);
}

internal sealed record CompactRelationObservation(
    int Ordinal,
    string SourceId,
    string? TargetId,
    string Partition,
    string RelationKind,
    string Resolution,
    string ResolutionMethod,
    string? UnresolvedReason,
    string? ObservedTargetText,
    CompactOriginMetadata Origin,
    ImmutableArray<CompactDocumentMetadata> Evidence);

internal sealed record CompactPostingMaps(
    ImmutableArray<CompactPostingList> Projects,
    ImmutableArray<CompactPostingList> Sources,
    ImmutableArray<CompactPostingList> Targets,
    ImmutableArray<CompactPostingList> Kinds,
    ImmutableArray<CompactPostingList> Resolutions);

internal sealed record CompactUnknownGroup(
    string UnresolvedReason,
    string SourceId,
    string ObservedTargetText,
    int Count,
    bool HasProvenEntryPoint,
    int Impact,
    ImmutableArray<int> RelationOrdinals);

internal sealed record CompactNamedCount(string Name, int Count);
internal sealed record CompactQualityMetric(
    string Key,
    int Total,
    ImmutableArray<CompactNamedCount> Resolutions,
    ImmutableArray<CompactNamedCount> ResolutionMethods);
internal sealed record CompactSummaryMetrics(
    int IndexedEndpointCount,
    int MappedEntryPointCount,
    ImmutableArray<CompactQualityMetric> ByPartition,
    ImmutableArray<CompactQualityMetric> ByRelationKind);
internal sealed record CompactRetrievalIndexBuildResult(
    ImmutableArray<CompactDocumentMetadata> Documents,
    ImmutableArray<CompactOriginMetadata> Origins,
    CompactPostingMaps Postings,
    ImmutableArray<CompactUnknownGroup> UnknownGroups,
    CompactSummaryMetrics Metrics);
