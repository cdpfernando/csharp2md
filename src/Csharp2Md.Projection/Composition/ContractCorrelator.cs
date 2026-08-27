using Csharp2Md.Storage;

namespace Csharp2Md.Projection.Composition;

internal sealed record SharedContractOwner(
    string SolutionIdentity,
    string ArtifactKey,
    int Ordinal);

internal sealed record SharedContract(
    string ContractFactId,
    ImmutableArray<SharedContractOwner> Owners);

internal static class ContractCorrelator
{
    public static ImmutableArray<SharedContract> Match(IReadOnlyList<SolutionContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var ownersByContract = new Dictionary<string, List<SharedContractOwner>>(StringComparer.Ordinal);
        foreach (var contribution in contributions)
        {
            if (contribution.Contracts.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var contract in contribution.Contracts)
            {
                if (!ownersByContract.TryGetValue(contract.FactId, out var owners))
                {
                    owners = [];
                    ownersByContract[contract.FactId] = owners;
                }

                if (owners.Exists(owner =>
                    string.Equals(owner.SolutionIdentity, contribution.SolutionIdentity, StringComparison.Ordinal)))
                {
                    continue;
                }

                owners.Add(new SharedContractOwner(
                    contribution.SolutionIdentity,
                    contract.ArtifactKey,
                    contract.Ordinal));
            }
        }

        var shared = ImmutableArray.CreateBuilder<SharedContract>();
        foreach (var (factId, owners) in ownersByContract)
        {
            var solutionCount = owners
                .Select(static owner => owner.SolutionIdentity)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (solutionCount < 2)
            {
                continue;
            }

            var ordered = owners
                .OrderBy(static owner => owner.SolutionIdentity, StringComparer.Ordinal)
                .ToImmutableArray();
            shared.Add(new SharedContract(factId, ordered));
        }

        return shared.ToImmutable();
    }
}
