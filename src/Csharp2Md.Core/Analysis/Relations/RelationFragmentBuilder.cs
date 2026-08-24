using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Analysis.Relations;

/// <summary>
/// What building the resolved relation fragment produced: the validated solution-level fragment, or
/// nothing plus the validator's own diagnostics. A run with no relations yields neither.
/// </summary>
internal sealed record RelationFragmentResult(
    ValidatedFactFragment? Fragment,
    ImmutableArray<AnalysisDiagnostic> Diagnostics)
{
    public static RelationFragmentResult Empty { get; } = new(null, []);
}

/// <summary>
/// Turns a <see cref="RelationResolution"/> into the run's one solution-level relation fragment and runs
/// it through the identical validate path every other fragment takes. A failure here is a structural
/// failure with the same diagnostics and the same meaning it would have on a document or the database
/// fragment - no new failure semantics are introduced. Structurally mirrors <c>DatabaseFragmentBuilder</c>,
/// including its empty-resolution short circuit; unlike it, this fragment's facts reference source and
/// target identities that live in other fragments entirely (symbol facts, database object/column facts),
/// so <paramref name="knownFactIds"/> must be supplied explicitly for <c>C2M-FV-002</c> to see them.
/// </summary>
internal static class RelationFragmentBuilder
{
    public static RelationFragmentResult Build(
        RelationResolution resolution, IReadOnlySet<FactId> knownFactIds, FragmentValidationFunc validate)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(knownFactIds);
        ArgumentNullException.ThrowIfNull(validate);

        if (resolution.Facts.IsEmpty)
        {
            return RelationFragmentResult.Empty;
        }

        var validation = validate(FactValidationInput.Create(
            resolution.Facts.Select(static fact => (IFact)fact),
            diagnostics: resolution.Diagnostics,
            documents: resolution.Documents,
            knownFactIds: knownFactIds));

        return new RelationFragmentResult(validation.Fragment, validation.ValidationDiagnostics);
    }
}
