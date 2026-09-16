using System.Security.Cryptography;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding.Rendering;

internal sealed record MachineArtifactSet(PackageManifest Manifest, ImmutableArray<PlannedArtifact> Artifacts);
internal sealed record NavigationIndexData(string ArtifactPath, ImmutableArray<NavigationIndexEntry> Entries);
internal sealed record NavigationIndexEntry(string Key, ImmutableArray<int> Ordinals, ImmutableArray<DependencyCategory> Categories, bool HasReachableSet);

internal static class MachineArtifactWriter
{
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
            var rootHandles = LocalTableBuilder.Build(solution.Solution.CanonicalKey, solution.Roots.Select(root => root.Value));
            var entitiesPath = $"{prefix}/graph/entities.000000.json";
            var dependenciesPath = $"{prefix}/measures/dependencies.000000.json";
            var measuresPath = $"{prefix}/measures/summary.json";
            Add(artifacts, $"{prefix}/tables/identities.000000.json", ArtifactFamily.Table, ImmutableArray.Create(solution.Solution), 1);
            Add(artifacts, entitiesPath, ArtifactFamily.Graph, solution.Roots, solution.Roots.Length);
            Add(artifacts, dependenciesPath, ArtifactFamily.Measure, solution.Dependencies, solution.Dependencies.Length);
            Add(artifacts, measuresPath, ArtifactFamily.Measure, solution.Measures, solution.Measures.Length);

            var indexes = IndexPaths(prefix).ToImmutableArray();
            foreach (var index in indexes)
            {
                AddIndex(artifacts, index, solution, dependenciesPath, measuresPath);
            }

            var roots = ImmutableArray.CreateBuilder<RootManifestEntry>();
            foreach (var root in solution.Roots)
            {
                var handle = rootHandles.Resolve(root.Value).Value;
                roots.Add(new RootManifestEntry(root.Value, handle, $"{entitiesPath}#{handle}", $"{prefix}/markdown/components/{handle}.md"));
            }

            manifestSolutions.Add(new SolutionManifestEntry(
                solutionId,
                solution.Solution.LogicalRelativePath,
                roots.OrderBy(root => root.Handle, StringComparer.Ordinal).ToImmutableArray(),
                indexes,
                Journeys()));
        }

        var manifest = new PackageManifest(
            PackageManifest.TokenEstimatorName,
            PackageManifest.TokenDivisorValue,
            includeTests,
            manifestSolutions.OrderBy(solution => solution.Id.Value, StringComparer.Ordinal).ToImmutableArray());
        Add(artifacts, "manifest.json", ArtifactFamily.Manifest, manifest, 1);
        return new MachineArtifactSet(manifest, artifacts.OrderBy(artifact => artifact.Path.Value, StringComparer.Ordinal).ToImmutableArray());
    }

    private static IEnumerable<IndexManifestEntry> IndexPaths(string prefix)
    {
        foreach (var kind in Enum.GetValues<NavigationIndexKind>())
        {
            yield return new IndexManifestEntry(kind, $"{prefix}/indexes/{IndexName(kind)}.json");
        }
    }

    private static void AddIndex(ImmutableArray<PlannedArtifact>.Builder artifacts, IndexManifestEntry index, SolutionRetrievalModel solution, string dependenciesPath, string measuresPath)
    {
        switch (index.Kind)
        {
            case NavigationIndexKind.Identity:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, ImmutableArray.Create(solution.Solution), 1);
                break;
            case NavigationIndexKind.Roots:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, solution.Roots, solution.Roots.Length);
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
                var evidence = solution.RetainedGraph?.Evidence ?? [];
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, evidence, evidence.Length);
                break;
            case NavigationIndexKind.Measures:
                var measureIndex = BuildMeasuresIndex(measuresPath, solution.Measures);
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, measureIndex, measureIndex.Entries.Length);
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
        Add(artifacts, indexPath, ArtifactFamily.Index, data, data.Entries.Length);
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

    private static void Add<T>(ImmutableArray<PlannedArtifact>.Builder artifacts, string path, ArtifactFamily family, T value, int records)
    {
        var bytes = CanonicalJson.Write(value);
        artifacts.Add(new PlannedArtifact(new RelativeArtifactPath(path), family, bytes, records, Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan()))));
    }
}
