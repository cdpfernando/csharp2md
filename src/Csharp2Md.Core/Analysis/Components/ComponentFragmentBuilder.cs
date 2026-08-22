using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Analysis.Components;

/// <summary>
/// What building the component fragment produced: the validated solution-level fragment plus the
/// <see cref="ComponentFact"/>s it carries, or nothing plus the validator's own diagnostics. A run with
/// no projects yields neither. <see cref="Components"/> is carried separately from <see cref="Fragment"/>
/// so a consumer like <c>GraphNodeIndex.Build</c> does not have to re-filter the fragment's facts by type.
/// </summary>
internal sealed record ComponentFragmentResult(
    ValidatedFactFragment? Fragment,
    ImmutableArray<ComponentFact> Components,
    ImmutableArray<AnalysisDiagnostic> Diagnostics)
{
    public static ComponentFragmentResult Empty { get; } = new(null, [], []);
}

/// <summary>
/// Mints one <see cref="ComponentFact"/> per <see cref="ProjectFact"/> and validates them into the run's
/// single solution-level component fragment. Structurally mirrors <c>RelationFragmentBuilder</c>: the same
/// empty-input short circuit, the same <c>validate(FactValidationInput.Create(...))</c> call, and the same
/// requirement that the caller supply <paramref name="knownFactIds"/> explicitly, because a component's
/// only reference - its owning project's id - lives in a fragment this builder never sees.
/// </summary>
internal static class ComponentFragmentBuilder
{
    private const string ComponentKind = "project";
    private const string EngineId = "csharp2md.components";
    private const string EngineVersion = "1";

    public static ComponentFragmentResult Build(
        IEnumerable<ProjectFact> projects, IReadOnlySet<FactId> knownFactIds, FragmentValidationFunc validate)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(knownFactIds);
        ArgumentNullException.ThrowIfNull(validate);

        var materialized = projects.ToImmutableArray();
        if (materialized.IsEmpty)
        {
            return ComponentFragmentResult.Empty;
        }

        var seenComponentIds = new HashSet<string>(StringComparer.Ordinal);
        var components = materialized.Select(project => Component(project, seenComponentIds)).ToImmutableArray();

        var validation = validate(FactValidationInput.Create(
            components.Select(static component => (IFact)component),
            knownFactIds: knownFactIds));

        return new ComponentFragmentResult(validation.Fragment, components, validation.ValidationDiagnostics);
    }

    private static ComponentFact Component(ProjectFact project, HashSet<string> seenComponentIds)
    {
        var componentId = ComponentFactId.Create(ComponentKind, [project.ProjectId.ToFactId()]);
        if (!seenComponentIds.Add(componentId.Value))
        {
            throw new InvalidOperationException(
                $"Duplicate component identity '{componentId.Value}' produced for project '{project.ProjectId.Value}'.");
        }

        // COMP-04: resolution is mirrored from the owning project, never hardcoded - a live syntax-only
        // run reports every project as Syntactic, so a hardcoded Exact would claim proof the project fact
        // itself does not have. Evidence stays empty: a .csproj is not a DocumentFact, so no line range
        // exists to point at.
        var header = FactHeader.Create(
            componentId.ToFactId(),
            FactKind.Component,
            project.Header.Resolution,
            [new FactProvenance(EngineId, EngineVersion)]);

        return new ComponentFact(header, componentId, ComponentKind, [project.ProjectId]);
    }
}
