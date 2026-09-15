using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding.Rendering;

internal static class MarkdownRenderer
{
    internal static ImmutableArray<PlannedArtifact> Render(RetrievalModel model, PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(manifest);
        var artifacts = ImmutableArray.CreateBuilder<PlannedArtifact>();
        Add(artifacts, "markdown/index.md", Summary(model, manifest), 1);
        var solutions = SolutionsById(model);
        foreach (var manifestSolution in manifest.Solutions)
        {
            var solution = solutions[manifestSolution.Id];
            foreach (var root in manifestSolution.Roots.OrderBy(root => root.MarkdownPath, StringComparer.Ordinal))
            {
                Add(artifacts, root.MarkdownPath, EntityPage(solution, root), 1);
            }
        }

        return artifacts.ToImmutable();
    }

    private static string Summary(RetrievalModel model, PackageManifest manifest)
    {
        var lines = new List<string> { "# Knowledge package", "", "## Components and Deployment Units" };
        lines.AddRange(manifest.Solutions.SelectMany(solution => solution.Roots.Select(root => (solution.Id, Root: root))).OrderBy(item => item.Id.Value, StringComparer.Ordinal).ThenBy(item => item.Root.DisplayName, StringComparer.Ordinal).Select(item => $"- [{Escape(item.Root.DisplayName)}]({item.Root.MarkdownPath})"));
        lines.AddRange(["", "## Top fan-in and fan-out"]);
        lines.AddRange(model.Solutions.SelectMany(solution => solution.Measures).OrderByDescending(measure => measure.FanIn).ThenBy(measure => measure.Entity.Value, StringComparer.Ordinal).Take(5).Select(measure => $"- {Escape(measure.Entity.Value)}: fan-in {measure.FanIn}, fan-out {measure.FanOut}"));
        lines.AddRange(["", "## Cycles"]);
        lines.AddRange(model.Solutions.SelectMany(solution => solution.Measures).SelectMany(measure => measure.Cycles).Distinct().OrderBy(cycle => cycle.Value, StringComparer.Ordinal).Select(cycle => $"- {Escape(cycle.Value)}"));
        lines.AddRange(["", "## Journeys"]);
        lines.AddRange(manifest.Solutions.SelectMany(solution => solution.Journeys.Select(journey => (Solution: solution, Journey: journey))).OrderBy(item => item.Solution.Id.Value, StringComparer.Ordinal).ThenBy(item => item.Journey.Kind).Select(item => $"- {item.Solution.Id.Value}: {item.Journey.Kind} via {item.Journey.EntryIndex}"));
        return string.Join('\n', lines) + "\n";
    }

    private static string EntityPage(SolutionRetrievalModel model, RootManifestEntry root)
    {
        var entity = root.DisplayName;
        var outgoing = model.Dependencies.Where(dependency => dependency.Source.Value == entity).OrderBy(dependency => dependency.Target.Value, StringComparer.Ordinal).ToArray();
        var incoming = model.Dependencies.Where(dependency => dependency.Target.Value == entity).OrderBy(dependency => dependency.Source.Value, StringComparer.Ordinal).ToArray();
        var measure = model.Measures.SingleOrDefault(value => value.Entity.Value == entity);
        var lines = new List<string> { $"# {Escape(entity)}", "", "## Outgoing" };
        lines.AddRange(outgoing.Select(dependency => $"- {Escape(dependency.Target.Value)} ({dependency.Category})"));
        lines.AddRange(["", "## Incoming"]);
        lines.AddRange(incoming.Select(dependency => $"- {Escape(dependency.Source.Value)} ({dependency.Category})"));
        lines.AddRange(["", "## Measures", $"- fan-in: {measure?.FanIn ?? 0}", $"- fan-out: {measure?.FanOut ?? 0}", "", "## Effects and gaps"]);
        lines.AddRange(measure?.ReverseImpact.OrderBy(impact => impact.Entity.Value, StringComparer.Ordinal).Select(impact => $"- impact: {Escape(impact.Entity.Value)} at depth {impact.Depth}") ?? []);
        lines.AddRange(measure is null ? [] : [ $"- candidate gaps: {measure.Gaps.Candidate}", $"- unknown gaps: {measure.Gaps.Unknown}", $"- open-frontier gaps: {measure.Gaps.OpenFrontier}" ]);
        return string.Join('\n', lines) + "\n";
    }

    private static void Add(ImmutableArray<PlannedArtifact>.Builder artifacts, string path, string text, int records)
    {
        var payload = Encoding.UTF8.GetBytes(text).ToImmutableArray();
        artifacts.Add(new PlannedArtifact(new RelativeArtifactPath(path), ArtifactFamily.Markdown, payload, records, Convert.ToHexStringLower(SHA256.HashData(payload.AsSpan()))));
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);

    private static IReadOnlyDictionary<SolutionId, SolutionRetrievalModel> SolutionsById(RetrievalModel model)
    {
        var registry = new PublicIdRegistry();
        return model.Solutions.ToDictionary(solution => registry.RegisterSolution(solution.Solution));
    }
}
