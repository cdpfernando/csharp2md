using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Composition;

internal sealed record NamedIdentityEntry(
    string FactId,
    string SolutionIdentity,
    string ArtifactKey,
    int Ordinal);

internal sealed record NamedIdentityGroup(
    string CanonicalName,
    string? SharedIdentity,
    ImmutableArray<NamedIdentityEntry> Entries);

internal static class NamedIdentityGrouping
{
    internal const string NotProven = "not-proven";

    public static ImmutableArray<NamedIdentityGroup> Group(
        IEnumerable<(string SolutionIdentity, ContributedNamedIdentity Identity)> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);

        var grouped = new Dictionary<string, List<NamedIdentityEntry>>(StringComparer.Ordinal);
        foreach (var (solutionIdentity, identity) in identities)
        {
            if (!grouped.TryGetValue(identity.Name, out var entries))
            {
                entries = [];
                grouped[identity.Name] = entries;
            }

            entries.Add(new NamedIdentityEntry(
                identity.FactId,
                solutionIdentity,
                identity.ArtifactKey,
                identity.Ordinal));
        }

        return grouped
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => ToGroup(pair.Key, pair.Value))
            .ToImmutableArray();
    }

    private static NamedIdentityGroup ToGroup(string canonicalName, List<NamedIdentityEntry> entries)
    {
        var ordered = entries
            .OrderBy(static entry => entry.SolutionIdentity, StringComparer.Ordinal)
            .ThenBy(static entry => entry.FactId, StringComparer.Ordinal)
            .ToImmutableArray();
        var solutionCount = ordered
            .Select(static entry => entry.SolutionIdentity)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new NamedIdentityGroup(
            canonicalName,
            solutionCount >= 2 ? NotProven : null,
            ordered);
    }
}

internal static class GlobalComponentCatalog
{
    public static ImmutableArray<NamedIdentityGroup> Group(IReadOnlyList<SolutionContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        return NamedIdentityGrouping.Group(Enumerate(contributions));
    }

    private static IEnumerable<(string SolutionIdentity, ContributedNamedIdentity Identity)> Enumerate(
        IReadOnlyList<SolutionContribution> contributions)
    {
        foreach (var contribution in contributions)
        {
            if (!contribution.Components.IsDefaultOrEmpty)
            {
                foreach (var component in contribution.Components)
                {
                    yield return (contribution.SolutionIdentity, component);
                }
            }

            if (!contribution.DeploymentUnits.IsDefaultOrEmpty)
            {
                foreach (var unit in contribution.DeploymentUnits)
                {
                    yield return (contribution.SolutionIdentity, unit);
                }
            }
        }
    }
}
