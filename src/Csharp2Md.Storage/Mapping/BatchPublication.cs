using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Mapping;

internal static class BatchPublication
{
    internal static (ImmutableArray<StagedFragment> Fragments, BatchManifestEnvelope Envelope) Prepare(
        ImmutableArray<BatchSolutionRecord> solutions,
        IReadOnlyDictionary<string, SolutionContribution> contributions,
        IBatchComposer? composer,
        Func<string, bool>? packagePresent = null)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var (resolved, selected) = ResolveContributions(solutions, contributions, packagePresent);
        var view = new BatchView(resolved, selected);
        var fragments = composer?.Compose(view) ?? [];
        BatchValidator.Validate(view, fragments);
        return (fragments, BatchManifestBuilder.From(view, fragments));
    }

    private static (ImmutableArray<BatchSolutionRecord> Solutions, ImmutableArray<SolutionContribution> Selected)
        ResolveContributions(
            ImmutableArray<BatchSolutionRecord> solutions,
            IReadOnlyDictionary<string, SolutionContribution> contributions,
            Func<string, bool>? packagePresent)
    {
        if (solutions.IsDefaultOrEmpty || contributions.Count == 0)
        {
            return (solutions, []);
        }

        var resolved = ImmutableArray.CreateBuilder<BatchSolutionRecord>(solutions.Length);
        var selected = ImmutableArray.CreateBuilder<SolutionContribution>();
        foreach (var record in solutions)
        {
            if (!contributions.TryGetValue(record.Identity.Value, out var contribution))
            {
                resolved.Add(record);
                continue;
            }

            if (packagePresent is not null && !packagePresent(record.Identity.Value))
            {
                resolved.Add(record with { Status = PublicationStatus.Unpublished });
                continue;
            }

            resolved.Add(record);
            selected.Add(contribution);
        }

        return (resolved.MoveToImmutable(), selected.ToImmutable());
    }
}
