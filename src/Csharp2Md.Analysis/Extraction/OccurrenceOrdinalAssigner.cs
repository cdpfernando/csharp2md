using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Analysis.Extraction;

internal static class OccurrenceOrdinalAssigner
{
    public static ImmutableArray<Observation> Assign(IEnumerable<ObservationDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        var sorted = drafts
            .OrderBy(static draft => draft.Locator.RelativePath, StringComparer.Ordinal)
            .ThenBy(static draft => draft.Locator.Span.StartLine)
            .ThenBy(static draft => draft.Locator.Span.StartColumn)
            .ThenBy(static draft => draft.Locator.Span.EndLine)
            .ThenBy(static draft => draft.Locator.Span.EndColumn)
            .ToArray();

        var ordinals = new Dictionary<OrdinalGroup, int>();
        var assigned = ImmutableArray.CreateBuilder<Observation>(sorted.Length);
        var extractorVersion = new ExtractorVersion(1);
        foreach (var draft in sorted)
        {
            var group = new OrdinalGroup(draft.Owner, draft.Kind, draft.Payload);
            var ordinal = ordinals.GetValueOrDefault(group) + 1;
            ordinals[group] = ordinal;
            assigned.Add(
                Observation.Create(
                    draft.Owner,
                    draft.Kind,
                    draft.Payload,
                    ordinal,
                    draft.Locator,
                    draft.ExtractionMethod,
                    draft.Diagnostic,
                    draft.DocumentHash,
                    extractorVersion));
        }

        return assigned.ToImmutable();
    }

    private readonly record struct OrdinalGroup(
        FactReference Owner,
        ObservationKind Kind,
        NormalizedPayload Payload);
}
