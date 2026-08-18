using Csharp2Md.Core.Analysis.Semantics.MSBuild;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Semantics;

internal sealed record ProjectFactEnrichmentResult(
    ProjectFact Project,
    ImmutableArray<TargetFact> Targets,
    ImmutableArray<AnalysisDiagnostic> Diagnostics);

internal static class ProjectFactEnricher
{
    private static readonly FactProvenance SemanticProvenance = new("csharp2md.semantic", "3.0.0");

    public static ProjectFactEnrichmentResult Enrich(
        ProjectFact syntacticProject,
        ProjectEvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(syntacticProject);
        ArgumentNullException.ThrowIfNull(evaluation);
        if (syntacticProject.ProjectId != evaluation.ProjectId)
        {
            throw new ArgumentException("Evaluation and syntactic project identities must match.", nameof(evaluation));
        }

        var targets = evaluation.Targets
            .OrderBy(static target => target.TargetId.Value, StringComparer.Ordinal)
            .Select(target => CreateTargetFact(evaluation.ProjectId, target))
            .ToImmutableArray();
        var resolution = ProjectResolution(evaluation.Targets);
        var diagnosticIds = evaluation.Diagnostics
            .Select(static diagnostic => diagnostic.Id)
            .Concat(evaluation.Targets.SelectMany(static target => target.Diagnostics.Select(static diagnostic => diagnostic.Id)))
            .Distinct()
            .OrderBy(static id => id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var header = FactHeader.Create(
            syntacticProject.Header.Id,
            FactKind.Project,
            resolution,
            syntacticProject.Header.Provenance.Append(SemanticProvenance),
            syntacticProject.Header.Evidence,
            syntacticProject.Header.DiagnosticIds.Concat(diagnosticIds));
        var project = syntacticProject with
        {
            Header = header,
            TargetIds = targets.Select(static target => target.TargetId).ToImmutableArray(),
            Evaluation = new ProjectEvaluationDetails(
                evaluation.DeclaredSdk,
                evaluation.EvaluatedImports.Order(StringComparer.Ordinal).ToImmutableArray(),
                evaluation.TargetFrameworks.Order(StringComparer.Ordinal).ToImmutableArray(),
                "semantic",
                resolution,
                RestorePerformed: false,
                Isolation: "none"),
        };
        return new ProjectFactEnrichmentResult(
            project,
            targets,
            evaluation.Diagnostics
                .Concat(evaluation.Targets.SelectMany(static target => target.Diagnostics))
                .GroupBy(static diagnostic => diagnostic.Id)
                .Select(static group => group.First())
                .Order()
                .ToImmutableArray());
    }

    private static TargetFact CreateTargetFact(
        Facts.Identity.ProjectFactId projectId,
        EvaluatedTarget target)
    {
        var resolution = target.Succeeded ? FactResolution.Exact : FactResolution.Syntactic;
        var header = FactHeader.Create(
            target.TargetId.ToFactId(),
            FactKind.Target,
            resolution,
            [SemanticProvenance],
            diagnosticIds: target.Diagnostics.Select(static diagnostic => diagnostic.Id));
        var details = target.Succeeded
            ? new TargetEvaluationDetails(
                Property(target, "OutputType"),
                Property(target, "AssemblyName"),
                Property(target, "RootNamespace"),
                Items(target, "Compile"),
                Items(target, "ProjectReference"),
                Items(target, "PackageReference"),
                Items(target, "Reference"),
                Split(Property(target, "DefineConstants")),
                Property(target, "LangVersion"),
                Property(target, "Nullable"),
                Items(target, "Analyzer"))
            : new TargetEvaluationDetails(
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                [],
                [],
                [],
                [],
                string.Empty,
                string.Empty,
                []);
        return new TargetFact(header, target.TargetId, projectId, target.TargetFramework, details);
    }

    private static FactResolution ProjectResolution(ImmutableArray<EvaluatedTarget> targets) =>
        targets.IsEmpty || targets.All(static target => !target.Succeeded)
            ? FactResolution.Syntactic
            : targets.All(static target => target.Succeeded)
                ? FactResolution.Exact
                : FactResolution.Partial;

    private static string Property(EvaluatedTarget target, string name) =>
        target.Properties.GetValueOrDefault(name, string.Empty);

    private static ImmutableArray<string> Items(EvaluatedTarget target, string name) =>
        target.Items.GetValueOrDefault(name, [])
            .Select(static item => item.Identity.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

    private static ImmutableArray<string> Split(string value) =>
        value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
}
