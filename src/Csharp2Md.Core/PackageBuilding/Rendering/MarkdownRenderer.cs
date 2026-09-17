using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding.Rendering;

internal static class MarkdownRenderer
{
    private const string SummaryPage = "markdown/index.md";

    internal static ImmutableArray<PlannedArtifact> Render(RetrievalModel model, PackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(manifest);
        var artifacts = ImmutableArray.CreateBuilder<PlannedArtifact>();
        var solutions = SolutionsById(model);
        var roots = manifest.Solutions.ToDictionary(
            entry => entry.Id,
            entry => MachineArtifactWriter.BuildRoots(solutions[entry.Id], entry.Id));
        var documents = manifest.Solutions.ToDictionary(
            entry => entry.Id,
            entry => MachineArtifactWriter.BuildDocuments(solutions[entry.Id].Dependencies, entry.Id));
        Add(artifacts, SummaryPage, Summary(model, manifest, roots, documents), 1);
        foreach (var manifestSolution in manifest.Solutions)
        {
            var solution = solutions[manifestSolution.Id];
            var index = roots[manifestSolution.Id];
            var documentIndex = documents[manifestSolution.Id];
            var pages = Pages(index, documentIndex);
            foreach (var root in index.Roots.OrderBy(root => index.MarkdownPath(root.Handle), StringComparer.Ordinal))
            {
                var page = index.MarkdownPath(root.Handle);
                Add(artifacts, page, EntityPage(solution, root.DisplayName, page, pages), 1);
            }

            foreach (var document in documentIndex.Documents.OrderBy(document => documentIndex.MarkdownPath(document.Handle), StringComparer.Ordinal))
            {
                var page = documentIndex.MarkdownPath(document.Handle);
                Add(artifacts, page, EntityPage(solution, document.DisplayName, page, pages), 1);
            }
        }

        return artifacts.ToImmutable();
    }

    private static string Summary(RetrievalModel model, PackageManifest manifest, IReadOnlyDictionary<SolutionId, RootsIndexData> roots, IReadOnlyDictionary<SolutionId, DocumentsIndexData> documents)
    {
        var lines = new List<string> { "# Knowledge package", "", "## Components and Deployment Units" };
        lines.AddRange(manifest.Solutions.SelectMany(solution => roots[solution.Id].Roots.Select(root => (solution.Id, Root: root, Index: roots[solution.Id]))).OrderBy(item => item.Id.Value, StringComparer.Ordinal).ThenBy(item => item.Root.DisplayName, StringComparer.Ordinal).Select(item => $"- [{Escape(item.Root.DisplayName)}]({Relative(SummaryPage, item.Index.MarkdownPath(item.Root.Handle))})"));
        lines.AddRange(["", "## Retained documents"]);
        lines.AddRange(manifest.Solutions.SelectMany(solution => documents[solution.Id].Documents.Select(document => (solution.Id, Document: document, Index: documents[solution.Id]))).OrderBy(item => item.Id.Value, StringComparer.Ordinal).ThenBy(item => item.Document.DisplayName, StringComparer.Ordinal).Select(item => $"- [{Escape(item.Document.DisplayName)}]({Relative(SummaryPage, item.Index.MarkdownPath(item.Document.Handle))})"));
        lines.AddRange(["", "## Top fan-in and fan-out"]);
        lines.AddRange(model.Solutions.SelectMany(solution => solution.Measures).OrderByDescending(measure => measure.FanIn).ThenBy(measure => measure.Entity.Value, StringComparer.Ordinal).Take(5).Select(measure => $"- {Escape(measure.Entity.Value)}: fan-in {measure.FanIn}, fan-out {measure.FanOut}"));
        lines.AddRange(["", "## Cycles"]);
        lines.AddRange(model.Solutions.SelectMany(solution => solution.Measures).SelectMany(measure => measure.Cycles).Distinct().OrderBy(cycle => cycle.Value, StringComparer.Ordinal).Select(cycle => $"- {Escape(cycle.Value)}"));
        lines.AddRange(["", "## Journeys"]);
        lines.AddRange(manifest.Solutions.SelectMany(solution => solution.Journeys.Select(journey => (Solution: solution, Journey: journey))).OrderBy(item => item.Solution.Id.Value, StringComparer.Ordinal).ThenBy(item => item.Journey.Kind).Select(item => $"- {item.Solution.Id.Value}: {item.Journey.Kind} via {item.Journey.EntryIndex}"));
        return string.Join('\n', lines) + "\n";
    }

    private static string EntityPage(SolutionRetrievalModel model, string entity, string page, IReadOnlyDictionary<string, string> pages)
    {
        var outgoing = model.Dependencies.Where(dependency => dependency.Source.Value == entity).OrderBy(dependency => dependency.Target.Value, StringComparer.Ordinal).ToArray();
        var incoming = model.Dependencies.Where(dependency => dependency.Target.Value == entity).OrderBy(dependency => dependency.Source.Value, StringComparer.Ordinal).ToArray();
        var measure = model.Measures.SingleOrDefault(value => value.Entity.Value == entity);
        var lines = new List<string> { $"# {Escape(entity)}", "", "## Outgoing" };
        lines.AddRange(outgoing.Select(dependency => $"- {Reference(dependency.Target.Value, page, pages)} ({dependency.Category})"));
        lines.AddRange(["", "## Incoming"]);
        lines.AddRange(incoming.Select(dependency => $"- {Reference(dependency.Source.Value, page, pages)} ({dependency.Category})"));
        lines.AddRange(["", "## Measures", $"- fan-in: {measure?.FanIn ?? 0}", $"- fan-out: {measure?.FanOut ?? 0}", "", "## Effects and gaps"]);
        lines.AddRange(measure?.ReverseImpact.OrderBy(impact => impact.Entity.Value, StringComparer.Ordinal).Select(impact => $"- impact: {Reference(impact.Entity.Value, page, pages)} at depth {impact.Depth}") ?? []);
        lines.AddRange(measure is null ? [] : [ $"- candidate gaps: {measure.Gaps.Candidate}", $"- unknown gaps: {measure.Gaps.Unknown}", $"- open-frontier gaps: {measure.Gaps.OpenFrontier}" ]);
        return string.Join('\n', lines) + "\n";
    }

    private static IReadOnlyDictionary<string, string> Pages(RootsIndexData roots, DocumentsIndexData documents)
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in roots.Roots) pages.TryAdd(root.DisplayName, roots.MarkdownPath(root.Handle));
        foreach (var document in documents.Documents) pages.TryAdd(document.DisplayName, documents.MarkdownPath(document.Handle));
        return pages;
    }

    // A row becomes a Markdown link only when its target has a written page. Anything else stays plain
    // text, so NAV-03's existing-link rule never degrades into a link to an artifact the package lacks.
    private static string Reference(string entity, string page, IReadOnlyDictionary<string, string> pages) =>
        pages.TryGetValue(entity, out var target) ? $"[{Escape(entity)}]({Relative(page, target)})" : Escape(entity);

    private static string Relative(string page, string target)
    {
        var from = page.Split('/');
        var to = target.Split('/');
        var shared = 0;
        while (shared < from.Length - 1 && shared < to.Length - 1 && string.Equals(from[shared], to[shared], StringComparison.Ordinal)) shared++;
        return string.Join('/', Enumerable.Repeat("..", from.Length - 1 - shared).Concat(to.Skip(shared)));
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
