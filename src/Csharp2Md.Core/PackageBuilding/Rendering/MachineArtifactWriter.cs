using System.Security.Cryptography;
using System.Text;
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
        var solutions = model.Solutions.OrderBy(solution => solution.Solution.CanonicalKey, StringComparer.Ordinal).ToArray();
        var roots = ImmutableArray.CreateBuilder<RootManifestEntry>();
        var indexes = ImmutableArray.CreateBuilder<IndexManifestEntry>();

        foreach (var solution in solutions)
        {
            var prefix = $"solutions/{SolutionDirectory(solution.Solution.CanonicalKey)}";
            var rootHandles = LocalTableBuilder.Build(solution.Solution.CanonicalKey, solution.Roots.Select(root => root.Value));
            var entitiesPath = $"{prefix}/graph/entities.000000.json";
            Add(artifacts, $"{prefix}/tables/identities.000000.json", ArtifactFamily.Table, ImmutableArray.Create(solution), 1);
            Add(artifacts, entitiesPath, ArtifactFamily.Graph, solution.Roots, solution.Roots.Length);
            Add(artifacts, $"{prefix}/graph/relations.000000.json", ArtifactFamily.Graph, model.Dependencies, model.Dependencies.Length);
            Add(artifacts, $"{prefix}/graph/gaps.000000.json", ArtifactFamily.Graph, model.Measures, model.Measures.Length);
            Add(artifacts, $"{prefix}/measures/dependencies.000000.json", ArtifactFamily.Measure, model.Dependencies, model.Dependencies.Length);
            Add(artifacts, $"{prefix}/measures/summary.json", ArtifactFamily.Measure, model.Measures, model.Measures.Length);

            foreach (var index in IndexPaths(prefix))
            {
                AddIndex(artifacts, index, model, solution);
                indexes.Add(new IndexManifestEntry(index.Name, index.Path));
            }

            foreach (var root in solution.Roots)
            {
                var handle = rootHandles.Resolve(root.Value).Value;
                roots.Add(new RootManifestEntry(root.Value, handle, $"{entitiesPath}#{handle}", $"{prefix}/markdown/components/{handle}.md"));
            }
        }

        var orderedIndexes = indexes.OrderBy(index => index.Name, StringComparer.Ordinal).ToImmutableArray();
        var manifest = new PackageManifest(
            PackageManifest.TokenEstimatorName,
            PackageManifest.TokenDivisorValue,
            includeTests,
            solutions.Select(solution => new SolutionManifestEntry(solution.Solution.LogicalRelativePath)).ToImmutableArray(),
            roots.OrderBy(root => root.Handle, StringComparer.Ordinal).ToImmutableArray(),
            orderedIndexes,
            [
                new JourneyManifestEntry(JourneyKind.Locate, orderedIndexes.First(index => index.Name == "roots").Path),
                new JourneyManifestEntry(JourneyKind.FollowFlow, orderedIndexes.First(index => index.Name == "outgoing").Path),
                new JourneyManifestEntry(JourneyKind.ReverseImpact, orderedIndexes.First(index => index.Name == "incoming").Path),
                new JourneyManifestEntry(JourneyKind.EvidenceDisposition, orderedIndexes.First(index => index.Name == "evidence").Path),
            ]);
        Add(artifacts, "manifest.json", ArtifactFamily.Manifest, manifest, 1);
        return new MachineArtifactSet(manifest, artifacts.OrderBy(artifact => artifact.Path.Value, StringComparer.Ordinal).ToImmutableArray());
    }

    private static IEnumerable<(string Name, string Path)> IndexPaths(string prefix)
    {
        foreach (var name in new[] { "identity", "roots", "outgoing", "incoming", "contracts", "persistence", "evidence", "measures" })
        {
            yield return (name, $"{prefix}/indexes/{name}.json");
        }
    }

    private static void AddIndex(ImmutableArray<PlannedArtifact>.Builder artifacts, (string Name, string Path) index, RetrievalModel model, SolutionNavigation solution)
    {
        switch (index.Name)
        {
            case "identity": Add(artifacts, index.Path, ArtifactFamily.Index, ImmutableArray.Create(solution), 1); break;
            case "roots": Add(artifacts, index.Path, ArtifactFamily.Index, solution.Roots, solution.Roots.Length); break;
            case "outgoing" or "incoming": Add(artifacts, index.Path, ArtifactFamily.Index, model.Dependencies, model.Dependencies.Length); break;
            case "measures": Add(artifacts, index.Path, ArtifactFamily.Index, model.Measures, model.Measures.Length); break;
            default: Add(artifacts, index.Path, ArtifactFamily.Index, model.Indexes, 1); break;
        }
    }

    private static void Add<T>(ImmutableArray<PlannedArtifact>.Builder artifacts, string path, ArtifactFamily family, T value, int records)
    {
        var bytes = CanonicalJson.Write(value);
        artifacts.Add(new PlannedArtifact(new RelativeArtifactPath(path), family, bytes, records, Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan()))));
    }

    private static string SolutionDirectory(string canonicalKey) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalKey)).AsSpan()[..10]);
}
