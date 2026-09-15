namespace Csharp2Md.Core.Analysis;

internal sealed class SolutionAnalysisException : InvalidOperationException
{
    public SolutionAnalysisException(string cause, SolutionIdentity solution, ProjectIdentity? project = null, AnalysisVariant? variant = null, Exception? innerException = null)
        : base($"analysis:{cause}", innerException) { Cause = cause; Solution = solution; Project = project; Variant = variant; }
    public string Cause { get; } public SolutionIdentity Solution { get; } public ProjectIdentity? Project { get; } public AnalysisVariant? Variant { get; }
}

internal static class SolutionAnalyzer
{
    internal static FactualGraph Assemble(SolutionIdentity solution, IEnumerable<FactualGraph> fragments, int extractedCount, int filteredCount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solution); ArgumentNullException.ThrowIfNull(fragments); cancellationToken.ThrowIfCancellationRequested();
        var graphs = fragments.ToArray();
        if (graphs.Any(g => g.Solution != solution)) throw new SolutionAnalysisException("solution-mismatch", solution);
        return new FactualGraph(solution,
            graphs.SelectMany(g => g.Entities).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Occurrences).OrderBy(x => x.EntityCanonicalKey, StringComparer.Ordinal).ThenBy(x => x.Project.CanonicalKey, StringComparer.Ordinal).ThenBy(x => CanonicalIdentity.VariantKey(x.Variant), StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Evidence).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Relations).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Gaps).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            graphs.SelectMany(g => g.Sources).DistinctBy(x => x.CanonicalKey).OrderBy(x => x.CanonicalKey, StringComparer.Ordinal).ToImmutableArray(),
            new ExtractionMeasurements(extractedCount, filteredCount));
    }
}
