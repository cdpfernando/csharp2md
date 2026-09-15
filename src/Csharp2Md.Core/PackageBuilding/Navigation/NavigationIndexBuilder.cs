using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.PackageBuilding.Navigation;

internal sealed record DirectLocator
{
    internal DirectLocator(string solutionKey, string artifactPath, int ordinal)
    { SolutionKey = CanonicalText.Require(solutionKey, nameof(solutionKey)); ArtifactPath = LogicalPath.RequireRelative(artifactPath, nameof(artifactPath)); ArgumentOutOfRangeException.ThrowIfNegative(ordinal); Ordinal = ordinal; }
    internal string SolutionKey { get; }
    internal string ArtifactPath { get; }
    internal int Ordinal { get; }
}

internal sealed class NavigationIndex
{
    private readonly IReadOnlyDictionary<string, DirectLocator> entries;
    internal NavigationIndex(IEnumerable<KeyValuePair<string, DirectLocator>> entries) => this.entries = entries.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => CanonicalText.Require(x.Key, nameof(entries)), x => x.Value, StringComparer.Ordinal);
    internal DirectLocator Resolve(string startKey) => entries.TryGetValue(startKey, out var locator) ? locator : throw new KeyNotFoundException($"No navigation locator exists for '{startKey}'.");
    internal IReadOnlyDictionary<string, DirectLocator> Entries => entries;
}

internal static class NavigationIndexBuilder
{
    internal static NavigationIndex Build(string solutionKey, IEnumerable<(string StartKey, string ArtifactPath, int Ordinal)> links) =>
        new NavigationIndex(links.Select(link => new KeyValuePair<string, DirectLocator>(link.StartKey, new DirectLocator(solutionKey, link.ArtifactPath, link.Ordinal))));
}
