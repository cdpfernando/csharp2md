using System.Security.Cryptography;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.PackageBuilding.Layout;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding.Rendering;

internal sealed record MachineArtifactSet(PackageManifest Manifest, ImmutableArray<PlannedArtifact> Artifacts);
internal sealed record NavigationIndexData(string ArtifactPath, ImmutableArray<NavigationIndexEntry> Entries);
internal sealed record NavigationIndexEntry(string Key, ImmutableArray<int> Ordinals, ImmutableArray<DependencyCategory> Categories, bool HasReachableSet);
internal sealed record DependencyPayload(ImmutableArray<StoredRelation> Relations, ImmutableArray<string> Evidence, ImmutableArray<AggregatedDependency> Dependencies);
internal sealed record StoredRelation(string CanonicalKey, string SourceCanonicalKey, string TargetCanonicalKey, string Category, ImmutableArray<EvidenceHandle> Evidence);
internal sealed record EvidenceIndexData(ImmutableArray<EvidenceShardEntry> Shards);
internal sealed record RootsIndexData(string CitationArtifact, string MarkdownPrefix, ImmutableArray<RootIndexEntry> Roots, string DocumentsIndexPath)
{
    internal string MachineCitation(string handle) => $"{CitationArtifact}#{handle}";
    internal string MarkdownPath(string handle) => $"{MarkdownPrefix}{handle}.md";
}

/// <summary>
/// Routes the Markdown pages of the retained documents. It is a separate artifact rather than a
/// section of the roots index so Locate keeps reading one short router: the roots index pays a
/// single path for it, not a row per document.
/// </summary>
internal sealed record DocumentsIndexData(string MarkdownPrefix, ImmutableArray<RootIndexEntry> Documents)
{
    internal string MarkdownPath(string handle) => $"{MarkdownPrefix}{handle}.md";
}
internal sealed record RootIndexEntry(string DisplayName, string Handle);
internal sealed record EvidenceShardEntry(string ArtifactPath, int FirstOrdinal, int Count);

internal static class MachineArtifactWriter
{
    // SPEC_DEVIATION: the evidence table packs at 32 KiB instead of the 64 KiB bulk target in design.md.
    // Reason: NAV-10 gives the evidence journey 25,000 tokens for the manifest, this router and one shard.
    // A 64 KiB shard leaves too little room for a real corpus manifest (Pitstop measured 102,050 of 100,000 bytes).
    private const int EvidenceShardTargetBytes = 32 * 1024;

    internal static MachineArtifactSet Write(RetrievalModel model, bool includeTests)
    {
        ArgumentNullException.ThrowIfNull(model);
        var artifacts = ImmutableArray.CreateBuilder<PlannedArtifact>();
        var registry = new PublicIdRegistry();
        var solutions = model.Solutions.OrderBy(solution => solution.Solution.CanonicalKey, StringComparer.Ordinal).ToArray();
        var manifestSolutions = ImmutableArray.CreateBuilder<SolutionManifestEntry>();

        foreach (var solution in solutions)
        {
            var solutionId = registry.RegisterSolution(solution.Solution);
            var prefix = $"solutions/{solutionId.Value}";
            var rootsIndex = BuildRoots(solution, solutionId);
            var entitiesPath = $"{prefix}/graph/entities.000000.json";
            var dependenciesPath = $"{prefix}/measures/dependencies.000000.json";
            var measuresPath = $"{prefix}/measures/summary.json";
            var entityKeys = EntityKeys(solution);
            var variantKeys = Keys(solution.Dependencies.SelectMany(dependency => dependency.Variants.Select(variant => variant.Value)));
            var cycleKeys = Keys(solution.Measures.SelectMany(measure => measure.Cycles.Select(cycle => cycle.Value)));
            var entities = LocalTableBuilder.Build(solution.Solution.CanonicalKey, entityKeys);
            var variants = LocalTableBuilder.Build(solution.Solution.CanonicalKey, variantKeys);
            var cycles = LocalTableBuilder.Build(solution.Solution.CanonicalKey, cycleKeys);
            AddCompact(artifacts, $"{prefix}/tables/identities.000000.json", ArtifactFamily.Table, ImmutableArray.Create(solution.Solution), 1);
            AddCompact(artifacts, $"{prefix}/tables/entities.000000.json", ArtifactFamily.Table, entityKeys, entityKeys.Length);
            AddCompact(artifacts, $"{prefix}/tables/variants.000000.json", ArtifactFamily.Table, variantKeys, variantKeys.Length);
            AddCompact(artifacts, $"{prefix}/tables/cycles.000000.json", ArtifactFamily.Table, cycleKeys, cycleKeys.Length);
            AddCompact(artifacts, entitiesPath, ArtifactFamily.Graph, solution.Roots, solution.Roots.Length);
            AddCompact(artifacts, dependenciesPath, ArtifactFamily.Measure, BuildDependencyPayload(solution, entities, variants), solution.Dependencies.Length);
            AddCompact(artifacts, measuresPath, ArtifactFamily.Measure, BuildMeasures(solution.Measures, entities, cycles), solution.Measures.Length);

            var documentsIndex = BuildDocuments(solution.Dependencies, solutionId);
            AddCompact(artifacts, rootsIndex.DocumentsIndexPath, ArtifactFamily.Index, documentsIndex, documentsIndex.Documents.Length);
            var evidenceIndex = AddEvidenceTable(artifacts, $"{prefix}/tables/evidence", solution.RetainedGraph?.Evidence ?? []);
            var indexes = IndexPaths(prefix).ToImmutableArray();
            foreach (var index in indexes)
            {
                AddIndex(artifacts, index, solution, dependenciesPath, measuresPath, evidenceIndex, rootsIndex);
            }

            manifestSolutions.Add(new SolutionManifestEntry(
                solutionId,
                solution.Solution.LogicalRelativePath,
                new RootsManifestEntry(indexes.Single(index => index.Kind == NavigationIndexKind.Roots).EntryPath, rootsIndex.Roots.Length),
                indexes,
                Journeys()));
        }

        var manifest = new PackageManifest(
            PackageManifest.TokenEstimatorName,
            PackageManifest.TokenDivisorValue,
            includeTests,
            manifestSolutions.OrderBy(solution => solution.Id.Value, StringComparer.Ordinal).ToImmutableArray());
        AddCompact(artifacts, "manifest.json", ArtifactFamily.Manifest, manifest, 1);
        return new MachineArtifactSet(manifest, artifacts.OrderBy(artifact => artifact.Path.Value, StringComparer.Ordinal).ToImmutableArray());
    }

    private static IEnumerable<IndexManifestEntry> IndexPaths(string prefix)
    {
        foreach (var kind in Enum.GetValues<NavigationIndexKind>())
        {
            yield return new IndexManifestEntry(kind, $"{prefix}/indexes/{IndexName(kind)}.json");
        }
    }

    private static void AddIndex(ImmutableArray<PlannedArtifact>.Builder artifacts, IndexManifestEntry index, SolutionRetrievalModel solution, string dependenciesPath, string measuresPath, EvidenceIndexData evidenceIndex, RootsIndexData rootsIndex)
    {
        switch (index.Kind)
        {
            case NavigationIndexKind.Identity:
                AddCompact(artifacts, index.EntryPath, ArtifactFamily.Index, ImmutableArray.Create(solution.Solution), 1);
                break;
            case NavigationIndexKind.Roots:
                AddCompact(artifacts, index.EntryPath, ArtifactFamily.Index, rootsIndex, rootsIndex.Roots.Length);
                break;
            case NavigationIndexKind.Outgoing:
                AddDependencyIndex(artifacts, index.EntryPath, dependenciesPath, solution.Dependencies, static dependency => dependency.Source.Value);
                break;
            case NavigationIndexKind.Incoming:
                AddDependencyIndex(artifacts, index.EntryPath, dependenciesPath, solution.Dependencies, static dependency => dependency.Target.Value);
                break;
            case NavigationIndexKind.Contracts:
                AddDependencyIndex(artifacts, index.EntryPath, dependenciesPath, solution.Dependencies, static dependency => dependency.Source.Value, DependencyCategory.Contract);
                break;
            case NavigationIndexKind.Persistence:
                AddDependencyIndex(artifacts, index.EntryPath, dependenciesPath, solution.Dependencies, static dependency => dependency.Source.Value, DependencyCategory.Persistence);
                break;
            case NavigationIndexKind.Evidence:
                AddCompact(artifacts, index.EntryPath, ArtifactFamily.Index, evidenceIndex, evidenceIndex.Shards.Length);
                break;
            case NavigationIndexKind.Measures:
                var measureIndex = BuildMeasuresIndex(measuresPath, solution.Measures);
                AddCompact(artifacts, index.EntryPath, ArtifactFamily.Index, measureIndex, measureIndex.Entries.Length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private static void AddDependencyIndex(
        ImmutableArray<PlannedArtifact>.Builder artifacts,
        string indexPath,
        string dependenciesPath,
        ImmutableArray<AggregatedDependency> dependencies,
        Func<AggregatedDependency, string> key,
        DependencyCategory? category = null)
    {
        var data = BuildDependencyIndex(dependenciesPath, dependencies, key, category);
        AddCompact(artifacts, indexPath, ArtifactFamily.Index, data, data.Entries.Length);
    }

    internal static NavigationIndexData BuildDependencyIndex(
        string dependenciesPath,
        ImmutableArray<AggregatedDependency> dependencies,
        Func<AggregatedDependency, string> key,
        DependencyCategory? category = null)
    {
        var entries = dependencies.Select((dependency, ordinal) => (dependency, ordinal))
            .Where(item => category is null || item.dependency.Category == category)
            .GroupBy(item => key(item.dependency), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new NavigationIndexEntry(
                group.Key,
                group.Select(item => item.ordinal).ToImmutableArray(),
                group.Select(item => item.dependency.Category).Distinct().Order().ToImmutableArray(),
                false))
            .ToImmutableArray();
        return new NavigationIndexData(dependenciesPath, entries);
    }

    internal static NavigationIndexData BuildMeasuresIndex(string measuresPath, ImmutableArray<ScopeMeasures> measures)
    {
        var entries = measures.Select((measure, ordinal) => (measure, ordinal))
            .GroupBy(item => item.measure.Entity.Value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new NavigationIndexEntry(
                group.Key,
                group.Select(item => item.ordinal).ToImmutableArray(),
                [],
                group.Any(item => !item.measure.ReverseImpact.IsDefaultOrEmpty)))
            .ToImmutableArray();
        return new NavigationIndexData(measuresPath, entries);
    }

    private static EvidenceIndexData AddEvidenceTable(ImmutableArray<PlannedArtifact>.Builder artifacts, string family, ImmutableArray<EvidenceRecord> evidence)
    {
        var records = evidence.DistinctBy(record => record.CanonicalKey, StringComparer.Ordinal)
            .OrderBy(record => record.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var byKey = records.ToDictionary(record => record.CanonicalKey, StringComparer.Ordinal);
        var shards = ShardPacker.Pack(family, records.Select(record => new ShardRecord(record.CanonicalKey, CanonicalJson.WriteCompact(record).AsSpan())), EvidenceShardTargetBytes);
        var entries = ImmutableArray.CreateBuilder<EvidenceShardEntry>(shards.Length);
        var ordinal = 0;
        foreach (var shard in shards)
        {
            var rows = shard.Records.Select(record => byKey[record.CanonicalKey]).ToImmutableArray();
            AddCompact(artifacts, shard.Path, ArtifactFamily.Table, rows, rows.Length);
            entries.Add(new EvidenceShardEntry(shard.Path, ordinal, rows.Length));
            ordinal += rows.Length;
        }

        return new EvidenceIndexData(entries.ToImmutable());
    }

    internal static RootsIndexData BuildRoots(SolutionRetrievalModel solution, SolutionId solutionId)
    {
        ArgumentNullException.ThrowIfNull(solution);
        var prefix = $"solutions/{solutionId.Value}";
        var keys = Keys(solution.Roots.Select(root => root.Value));
        var roots = keys.Select((key, ordinal) => new RootIndexEntry(key, LocalTableBuilder.HandleForOrdinal(ordinal))).ToImmutableArray();
        return new RootsIndexData($"{prefix}/graph/entities.000000.json", $"{prefix}/markdown/components/", roots, $"{prefix}/indexes/documents.json");
    }

    /// <summary>
    /// The retained documents are the endpoints of the document-scoped dependencies. A document earns an
    /// edge only through kept evidence, so this is exactly the cited set PKG-06 bounds, and it is derived
    /// from the dependencies alone so a rehydrated package rebuilds the same routing.
    /// </summary>
    internal static DocumentsIndexData BuildDocuments(ImmutableArray<AggregatedDependency> dependencies, SolutionId solutionId)
    {
        var keys = Keys(dependencies.IsDefaultOrEmpty
            ? []
            : dependencies.Where(static dependency => dependency.Scope == AggregationScope.Document)
                .SelectMany(static dependency => new[] { dependency.Source.Value, dependency.Target.Value }));
        return new DocumentsIndexData(
            $"solutions/{solutionId.Value}/markdown/documents/",
            keys.Select(static (key, ordinal) => new RootIndexEntry(key, LocalTableBuilder.HandleForOrdinal(ordinal))).ToImmutableArray());
    }

    internal static ImmutableArray<string> EntityKeys(SolutionRetrievalModel solution) =>
        Keys(solution.Dependencies.SelectMany(dependency => new[] { dependency.Source.Value, dependency.Target.Value })
            .Concat(solution.RetainedGraph?.Relations.SelectMany(relation => new[] { relation.SourceCanonicalKey, relation.TargetCanonicalKey }) ?? [])
            .Concat(solution.Measures.Select(measure => measure.Entity.Value))
            .Concat(solution.Measures.SelectMany(measure => measure.ReverseImpact.Select(target => target.Entity.Value))));

    private static ImmutableArray<string> Keys(IEnumerable<string> canonicalKeys) =>
        canonicalKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();

    private static ImmutableArray<ScopeMeasures> BuildMeasures(ImmutableArray<ScopeMeasures> measures, LocalTable entities, LocalTable cycles) =>
        measures.Select(measure => new ScopeMeasures(
            measure.Scope,
            new EntityHandle(entities.Resolve(measure.Entity.Value).Value),
            measure.FanIn,
            measure.FanOut,
            measure.CrossComponentEdges,
            measure.Cycles.Select(cycle => new CycleHandle(cycles.Resolve(cycle.Value).Value)).ToImmutableArray(),
            measure.ReverseImpact.Select(target => new ImpactTarget(new EntityHandle(entities.Resolve(target.Entity.Value).Value), target.Depth)).ToImmutableArray(),
            measure.Gaps)).ToImmutableArray();

    private static DependencyPayload BuildDependencyPayload(SolutionRetrievalModel solution, LocalTable entities, LocalTable variants)
    {
        var relationKeys = solution.Dependencies.SelectMany(dependency => dependency.Relations.Select(handle => handle.Value))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
        var facts = solution.RetainedGraph?.Relations.ToDictionary(relation => relation.CanonicalKey, StringComparer.Ordinal)
            ?? new Dictionary<string, FactualRelation>(StringComparer.Ordinal);
        var evidenceKeys = solution.Dependencies.SelectMany(dependency => dependency.Evidence.Select(handle => handle.Value))
            .Concat(facts.Values.SelectMany(relation => relation.EvidenceCanonicalKeys))
            .Concat(solution.RetainedGraph?.Evidence.Select(item => item.CanonicalKey) ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
        var retainedEvidence = solution.RetainedGraph?.Evidence.Select(item => item.CanonicalKey).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        if (evidenceKeys.Any(key => !retainedEvidence.Contains(key)))
            throw new InvalidOperationException("A dependency references missing confirmed evidence.");
        var relationTable = LocalTableBuilder.Build(solution.Solution.CanonicalKey, relationKeys);
        var evidenceTable = LocalTableBuilder.Build(solution.Solution.CanonicalKey, evidenceKeys);
        var relations = relationKeys.Select(key =>
        {
            if (!facts.TryGetValue(key, out var fact))
                throw new InvalidOperationException($"A dependency references missing confirmed relation '{key}'.");
            return new StoredRelation(
                key,
                entities.Resolve(fact.SourceCanonicalKey).Value,
                entities.Resolve(fact.TargetCanonicalKey).Value,
                fact.Category,
                fact.EvidenceCanonicalKeys.Select(evidence => new EvidenceHandle(evidenceTable.Resolve(evidence).Value)).ToImmutableArray());
        }).ToImmutableArray();
        var dependencies = solution.Dependencies.Select(dependency => new AggregatedDependency(
            dependency.Scope,
            new EntityHandle(entities.Resolve(dependency.Source.Value).Value),
            new EntityHandle(entities.Resolve(dependency.Target.Value).Value),
            dependency.Category,
            dependency.Nature,
            dependency.OccurrenceCount,
            dependency.Variants.Select(variant => new VariantHandle(variants.Resolve(variant.Value).Value)).ToImmutableArray(),
            dependency.Relations.Select(relation => new RelationHandle(relationTable.Resolve(relation.Value).Value)).ToImmutableArray(),
            dependency.Evidence.Select(evidence => new EvidenceHandle(evidenceTable.Resolve(evidence.Value).Value)).ToImmutableArray()))
            .ToImmutableArray();
        return new DependencyPayload(relations, evidenceKeys, dependencies);
    }

    private static ImmutableArray<JourneyManifestEntry> Journeys() =>
    [
        new JourneyManifestEntry(JourneyKind.Locate, NavigationIndexKind.Roots),
        new JourneyManifestEntry(JourneyKind.FollowFlow, NavigationIndexKind.Outgoing),
        new JourneyManifestEntry(JourneyKind.ReverseImpact, NavigationIndexKind.Incoming),
        new JourneyManifestEntry(JourneyKind.EvidenceDisposition, NavigationIndexKind.Evidence),
    ];

    private static string IndexName(NavigationIndexKind kind) => kind switch
    {
        NavigationIndexKind.Identity => "identity",
        NavigationIndexKind.Roots => "roots",
        NavigationIndexKind.Outgoing => "outgoing",
        NavigationIndexKind.Incoming => "incoming",
        NavigationIndexKind.Contracts => "contracts",
        NavigationIndexKind.Persistence => "persistence",
        NavigationIndexKind.Evidence => "evidence",
        NavigationIndexKind.Measures => "measures",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static void AddCompact<T>(ImmutableArray<PlannedArtifact>.Builder artifacts, string path, ArtifactFamily family, T value, int records)
    {
        var bytes = CanonicalJson.WriteCompact(value);
        artifacts.Add(new PlannedArtifact(new RelativeArtifactPath(path), family, bytes, records, Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan()))));
    }
}
