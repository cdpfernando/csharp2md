using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class BatchPublication
{
    internal static (ImmutableArray<StagedFragment> Fragments, BatchManifestEnvelope Envelope) Prepare(
        ImmutableArray<BatchSolutionRecord> solutions,
        IReadOnlyDictionary<string, SolutionContribution> contributions,
        IBatchComposer? composer)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var selected = SelectContributions(solutions, contributions);
        var view = new BatchView(solutions, selected);
        var fragments = composer?.Compose(view) ?? [];
        BatchValidator.Validate(view, fragments);
        return (fragments, BatchManifestBuilder.From(view, fragments));
    }

    private static ImmutableArray<SolutionContribution> SelectContributions(
        ImmutableArray<BatchSolutionRecord> solutions,
        IReadOnlyDictionary<string, SolutionContribution> contributions)
    {
        if (solutions.IsDefaultOrEmpty || contributions.Count == 0)
        {
            return [];
        }

        var selected = ImmutableArray.CreateBuilder<SolutionContribution>();
        foreach (var record in solutions)
        {
            if (contributions.TryGetValue(record.Identity.Value, out var contribution))
            {
                selected.Add(contribution);
            }
        }

        return selected.ToImmutable();
    }
}
