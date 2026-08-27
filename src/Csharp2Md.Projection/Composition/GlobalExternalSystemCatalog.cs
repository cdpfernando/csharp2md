using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Composition;

internal static class GlobalExternalSystemCatalog
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
            if (contribution.ExternalSystems.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var system in contribution.ExternalSystems)
            {
                yield return (contribution.SolutionIdentity, system);
            }
        }
    }
}
