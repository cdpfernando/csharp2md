using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.PackageBuilding.Retention;

internal static class RetentionPolicy
{
    internal static RetainedGraph Apply(FactualGraph graph, RetainedGraph closure, bool includeTests)
    {
        ArgumentNullException.ThrowIfNull(graph); ArgumentNullException.ThrowIfNull(closure);
        var retainedKeys = closure.Entities.Select(x => x.CanonicalKey).ToHashSet(StringComparer.Ordinal);
        var incoming = graph.Relations.Where(x => retainedKeys.Contains(x.TargetCanonicalKey)).ToArray();
        retainedKeys.UnionWith(incoming.Select(x => x.SourceCanonicalKey));
        var relations = closure.Relations.Concat(incoming).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var evidenceKeys = relations.SelectMany(x => x.EvidenceCanonicalKeys).ToHashSet(StringComparer.Ordinal);
        var evidence = graph.Evidence.Where(x => evidenceKeys.Contains(x.CanonicalKey)).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var citedDocuments = evidence.Select(x => x.DocumentCanonicalKey).ToHashSet(StringComparer.Ordinal);
        var sources = graph.Sources.Where(x => citedDocuments.Contains(x.CanonicalKey) && (includeTests || !x.IsTest)).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var permittedDocuments = sources.Select(x => x.CanonicalKey).ToHashSet(StringComparer.Ordinal);
        evidence = evidence.Where(x => permittedDocuments.Contains(x.DocumentCanonicalKey) || !graph.Sources.Any(s => s.CanonicalKey == x.DocumentCanonicalKey)).ToImmutableArray();
        var gaps = graph.Gaps.Where(x => x.AffectedEntityCanonicalKeys.Any(retainedKeys.Contains)).OrderByDescending(x => x.AffectedEntityCanonicalKeys.Count(retainedKeys.Contains)).ThenBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var entities = graph.Entities.Where(x => retainedKeys.Contains(x.CanonicalKey)).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray();
        var total = entities.Length + relations.Length + evidence.Length + gaps.Length + sources.Length;
        return new RetainedGraph(entities, relations, gaps, evidence, sources, new RetentionMeasurements(total, Math.Max(0, graph.Entities.Length + graph.Relations.Length + graph.Evidence.Length + graph.Gaps.Length + graph.Sources.Length - total), includeTests));
    }
}
