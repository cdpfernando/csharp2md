using System.Security.Cryptography;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding.Rendering;

internal sealed record MachineArtifactSet(PackageManifest Manifest, ImmutableArray<PlannedArtifact> Artifacts);

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
            Add(artifacts, $"{prefix}/tables/identities.000000.json", ArtifactFamily.Table, ImmutableArray.Create(solution.Solution), 1);
            Add(artifacts, entitiesPath, ArtifactFamily.Graph, solution.Roots, solution.Roots.Length);
            Add(artifacts, $"{prefix}/graph/relations.000000.json", ArtifactFamily.Graph, solution.Dependencies, solution.Dependencies.Length);
            Add(artifacts, $"{prefix}/graph/gaps.000000.json", ArtifactFamily.Graph, solution.Measures, solution.Measures.Length);
            Add(artifacts, $"{prefix}/measures/dependencies.000000.json", ArtifactFamily.Measure, solution.Dependencies, solution.Dependencies.Length);
            Add(artifacts, $"{prefix}/measures/summary.json", ArtifactFamily.Measure, solution.Measures, solution.Measures.Length);

            var indexes = IndexPaths(prefix).ToImmutableArray();
            foreach (var index in indexes)
            {
                AddIndex(artifacts, index, solution);
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

    private static void AddIndex(ImmutableArray<PlannedArtifact>.Builder artifacts, IndexManifestEntry index, SolutionRetrievalModel solution)
    {
        switch (index.Kind)
        {
            case NavigationIndexKind.Identity:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, ImmutableArray.Create(solution.Solution), 1);
                break;
            case NavigationIndexKind.Roots:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, solution.Roots, solution.Roots.Length);
                break;
            case NavigationIndexKind.Outgoing or NavigationIndexKind.Incoming:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, solution.Dependencies, solution.Dependencies.Length);
                break;
            case NavigationIndexKind.Contracts:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, solution.Dependencies.Where(dependency => dependency.Category == DependencyCategory.Contract).ToImmutableArray(), solution.Dependencies.Count(dependency => dependency.Category == DependencyCategory.Contract));
                break;
            case NavigationIndexKind.Persistence:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, solution.Dependencies.Where(dependency => dependency.Category == DependencyCategory.Persistence).ToImmutableArray(), solution.Dependencies.Count(dependency => dependency.Category == DependencyCategory.Persistence));
                break;
            case NavigationIndexKind.Evidence:
                var evidence = solution.RetainedGraph?.Evidence ?? [];
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, evidence, evidence.Length);
                break;
            case NavigationIndexKind.Measures:
                Add(artifacts, index.EntryPath, ArtifactFamily.Index, solution.Measures, solution.Measures.Length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
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
