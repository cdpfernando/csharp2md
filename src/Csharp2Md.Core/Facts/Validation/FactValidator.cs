using System.Collections.Frozen;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Facts.Validation;

internal static class FactValidator
{
    private static readonly FrozenSet<string> CompileTimeOnlyRelationKinds =
        new[] { "project-reference", "package-reference" }.ToFrozenSet(StringComparer.Ordinal);

    public static FactValidationResult Validate(FactValidationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<AnalysisDiagnostic>();
        var definedIds = input.Facts.Select(static fact => fact.Header.Id).ToArray();
        var availableIds = definedIds
            .Concat(input.Diagnostics.Select(static diagnostic => diagnostic.Id.ToFactId()))
            .Concat(input.KnownFactIds)
            .ToFrozenSet();
        var documentExtents = input.Documents.ToFrozenDictionary(static document => document.DocumentId);

        ValidateDuplicates(input.Facts, errors);
        foreach (var fact in input.Facts)
        {
            ValidateReferences(fact, availableIds, errors);
            ValidateEvidence(fact, documentExtents, errors);
            ValidateResolution(fact, errors);
            if (fact is RelationFact relation)
            {
                ValidateRelation(relation, errors);
            }
        }

        if (errors.Count != 0)
        {
            return new FactValidationResult(
                null,
                errors.Order().ToImmutableArray());
        }

        return new FactValidationResult(
            new ValidatedFactFragment(
                input.Facts.OrderBy(static fact => fact.Header.Id.Value, StringComparer.Ordinal).ToImmutableArray(),
                input.Diagnostics.Order().ToImmutableArray()),
            []);
    }

    private static void ValidateDuplicates(ImmutableArray<IFact> facts, List<AnalysisDiagnostic> errors)
    {
        foreach (var duplicate in facts.GroupBy(static fact => fact.Header.Id).Where(static group => group.Skip(1).Any()))
        {
            errors.Add(Error(duplicate.First(), "C2M-FV-001", "duplicate-id"));
        }
    }

    private static void ValidateReferences(IFact fact, FrozenSet<FactId> availableIds, List<AnalysisDiagnostic> errors)
    {
        var references = GetReferences(fact).Concat(fact.Header.DiagnosticIds.Select(static id => id.ToFactId()));
        foreach (var reference in references.Distinct().Where(reference => !availableIds.Contains(reference)))
        {
            errors.Add(Error(fact, "C2M-FV-002", "missing-reference", new DiagnosticData("reference", reference.Value)));
        }
    }

    private static IEnumerable<FactId> GetReferences(IFact fact) => fact switch
    {
        SolutionFact solution => solution.ProjectIds.Select(static id => id.ToFactId()),
        ProjectFact project => project.TargetIds.Select(static id => id.ToFactId())
            .Concat(project.DocumentIds.Select(static id => id.ToFactId())),
        TargetFact target => [target.ProjectId.ToFactId()],
        DocumentFact document => document.SymbolIds.Select(static id => id.ToFactId())
            .Append(document.ProjectId.ToFactId()),
        SourceSectionFact section => [section.DocumentId.ToFactId()],
        SymbolFact symbol => symbol.BaseAndInterfaceIds.Select(static id => id.ToFactId())
            .Append(symbol.DocumentId.ToFactId()),
        ComponentFact component => component.ProjectIds.Select(static id => id.ToFactId()),
        RelationFact relation when relation.TargetId is { } target => [relation.SourceId, target],
        RelationFact relation => [relation.SourceId],
        _ => [],
    };

    private static void ValidateEvidence(
        IFact fact,
        FrozenDictionary<DocumentFactId, DocumentExtent> documents,
        List<AnalysisDiagnostic> errors)
    {
        foreach (var evidence in fact.Header.Evidence)
        {
            if (!documents.TryGetValue(evidence.DocumentId, out var document) ||
                !string.Equals(document.RelativePath, evidence.RelativePath, StringComparison.Ordinal))
            {
                errors.Add(Error(fact, "C2M-FV-004", "invalid-evidence-document"));
                continue;
            }

            if (!IsInRange(evidence.StartLine, evidence.StartColumn, document.LineLengths) ||
                !IsInRange(evidence.EndLine, evidence.EndColumn, document.LineLengths))
            {
                errors.Add(Error(fact, "C2M-FV-004", "invalid-evidence-range"));
            }
        }
    }

    private static bool IsInRange(int line, int column, ImmutableArray<int> lineLengths) =>
        line <= lineLengths.Length && column <= lineLengths[line - 1] + 1;

    private static void ValidateResolution(IFact fact, List<AnalysisDiagnostic> errors)
    {
        if (fact is SymbolFact { ContainsErrorSymbol: true, Header.Resolution: FactResolution.Exact })
        {
            errors.Add(Error(fact, "C2M-FV-003", "exact-error-symbol"));
        }
    }

    private static void ValidateRelation(RelationFact relation, List<AnalysisDiagnostic> errors)
    {
        if (relation.IsRuntime && !relation.Header.Provenance.Any(static provenance => provenance.DetectorId is not null))
        {
            errors.Add(Error(relation, "C2M-FV-005", "runtime-detector-provenance"));
        }

        if (relation.IsRuntime && relation.Header.Evidence.IsEmpty)
        {
            errors.Add(Error(relation, "C2M-FV-005", "runtime-evidence"));
        }

        if (relation.IsRuntime && CompileTimeOnlyRelationKinds.Contains(relation.RelationKind))
        {
            errors.Add(Error(relation, "C2M-FV-006", "compile-time-reference-in-runtime-partition"));
        }

        if (relation.TargetId is null && string.IsNullOrWhiteSpace(relation.UnresolvedReason))
        {
            errors.Add(Error(relation, "C2M-FV-007", "missing-unresolved-reason"));
        }
    }

    private static AnalysisDiagnostic Error(IFact fact, string code, string rule, params DiagnosticData[] data) =>
        AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Error,
            DiagnosticStage.Validation,
            fact.Header.Id,
            $"Fact '{fact.Header.Id.Value}' violates validation rule '{rule}'.",
            [new DiagnosticData("rule", rule), .. data]);
}
