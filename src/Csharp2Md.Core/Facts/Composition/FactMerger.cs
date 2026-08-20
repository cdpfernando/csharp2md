using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Facts.Composition;

internal sealed record FactMergeResult(
    ImmutableArray<IFact> Facts,
    ImmutableArray<AnalysisDiagnostic> Diagnostics,
    ImmutableArray<AnalysisDiagnostic> StructuralDiagnostics)
{
    public bool IsValid => StructuralDiagnostics.IsEmpty;
}

internal static class FactMerger
{
    public static FactMergeResult Merge(
        IEnumerable<IFact> baseline,
        IEnumerable<IFact>? enrichment = null,
        IEnumerable<AnalysisDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var mergedDiagnostics = (diagnostics ?? [])
            .Distinct()
            .Order()
            .ToImmutableArray();
        var structuralDiagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        var merged = new Dictionary<FactId, IFact>();

        foreach (var candidate in baseline.Concat(enrichment ?? []))
        {
            ArgumentNullException.ThrowIfNull(candidate);
            if (!merged.TryGetValue(candidate.Header.Id, out var existing))
            {
                merged.Add(candidate.Header.Id, candidate);
                continue;
            }

            var result = MergeSameIdentity(existing, candidate, structuralDiagnostics);
            merged[candidate.Header.Id] = result;
        }

        var structural = structuralDiagnostics
            .Distinct()
            .Order()
            .ToImmutableArray();
        var allDiagnostics = mergedDiagnostics
            .AddRange(structural)
            .Distinct()
            .Order()
            .ToImmutableArray();
        var scoped = merged.Values
            .Select(fact => AttachScopedDiagnostics(fact, allDiagnostics))
            .ToDictionary(static fact => fact.Header.Id);
        RecomputeDocuments(scoped);

        return new FactMergeResult(
            scoped.Values.OrderBy(static fact => fact.Header.Id.Value, StringComparer.Ordinal).ToImmutableArray(),
            allDiagnostics,
            structural);
    }

    private static IFact MergeSameIdentity(
        IFact existing,
        IFact candidate,
        ImmutableArray<AnalysisDiagnostic>.Builder structuralDiagnostics)
    {
        if (existing.GetType() != candidate.GetType() || existing.Header.Kind != candidate.Header.Kind)
        {
            structuralDiagnostics.Add(Conflict(
                existing.Header.Id,
                "C2M-FM-001",
                "Facts with one identity have incompatible fact kinds."));
            return existing;
        }

        var claimsEqual = ClaimsEqual(existing, candidate);
        if (existing.Header.Resolution == candidate.Header.Resolution && !claimsEqual)
        {
            var exact = existing.Header.Resolution is FactResolution.Exact;
            structuralDiagnostics.Add(Conflict(
                existing.Header.Id,
                exact ? "C2M-FM-002" : "C2M-FM-003",
                exact
                    ? "Conflicting exact claims share one fact identity."
                    : "Conflicting equal-resolution claims share one fact identity."));
            return existing;
        }

        var winner = ResolutionRank(candidate.Header.Resolution) > ResolutionRank(existing.Header.Resolution)
            ? candidate
            : existing;
        var header = FactHeader.Create(
            winner.Header.Id,
            winner.Header.Kind,
            winner.Header.Resolution,
            existing.Header.Provenance.Concat(candidate.Header.Provenance),
            existing.Header.Evidence.Concat(candidate.Header.Evidence),
            existing.Header.DiagnosticIds.Concat(candidate.Header.DiagnosticIds));
        return WithHeader(winner, header);
    }

    private static IFact AttachScopedDiagnostics(IFact fact, ImmutableArray<AnalysisDiagnostic> diagnostics)
    {
        var scoped = diagnostics
            .Where(diagnostic => diagnostic.ScopeId == fact.Header.Id)
            .Select(static diagnostic => diagnostic.Id);
        var header = FactHeader.Create(
            fact.Header.Id,
            fact.Header.Kind,
            fact.Header.Resolution,
            fact.Header.Provenance,
            fact.Header.Evidence,
            fact.Header.DiagnosticIds.Concat(scoped));
        return WithHeader(fact, header);
    }

    private static void RecomputeDocuments(Dictionary<FactId, IFact> facts)
    {
        foreach (var document in facts.Values.OfType<DocumentFact>().ToArray())
        {
            var symbolFacts = document.SymbolIds
                .Select(symbolId => facts.GetValueOrDefault(symbolId.ToFactId()) as SymbolFact)
                .ToArray();
            var resolutions = symbolFacts
                .Select(static symbol => symbol?.Header.Resolution ?? FactResolution.Unresolved);
            var diagnosticIds = symbolFacts
                .OfType<SymbolFact>()
                .SelectMany(static symbol => symbol.Header.DiagnosticIds);
            var resolution = FactResolutionAlgebra.AggregateDocument(resolutions);
            var header = FactHeader.Create(
                document.Header.Id,
                document.Header.Kind,
                resolution,
                document.Header.Provenance,
                document.Header.Evidence,
                document.Header.DiagnosticIds.Concat(diagnosticIds));
            facts[document.Header.Id] = document with { Header = header };
        }
    }

    private static bool ClaimsEqual(IFact first, IFact second) => (first, second) switch
    {
        (SolutionFact left, SolutionFact right) =>
            left.Name == right.Name && left.ProjectIds.SequenceEqual(right.ProjectIds),
        (ProjectFact left, ProjectFact right) =>
            left.ProjectId == right.ProjectId
            && left.Name == right.Name
            && left.RelativePath == right.RelativePath
            && left.TargetIds.SequenceEqual(right.TargetIds)
            && left.DocumentIds.SequenceEqual(right.DocumentIds)
            && ProjectEvaluationEqual(left.Evaluation, right.Evaluation),
        (TargetFact left, TargetFact right) =>
            left.TargetId == right.TargetId
            && left.ProjectId == right.ProjectId
            && left.TargetFramework == right.TargetFramework
            && TargetEvaluationEqual(left.Evaluation, right.Evaluation),
        (DocumentFact left, DocumentFact right) =>
            left.DocumentId == right.DocumentId
            && left.ProjectId == right.ProjectId
            && left.RelativePath == right.RelativePath
            && left.Sections.Length == right.Sections.Length
            && left.Sections.Zip(right.Sections).All(static pair => ClaimsEqual(pair.First, pair.Second))
            && left.SymbolIds.SequenceEqual(right.SymbolIds),
        (SourceSectionFact left, SourceSectionFact right) =>
            left.DocumentId == right.DocumentId
            && left.SectionKind == right.SectionKind
            && left.OccurrenceOrdinal == right.OccurrenceOrdinal
            && left.StartOffset == right.StartOffset
            && left.Length == right.Length
            && left.Source == right.Source,
        (SymbolFact left, SymbolFact right) =>
            left.SymbolId == right.SymbolId
            && left.DocumentId == right.DocumentId
            && left.SymbolKind == right.SymbolKind
            && left.ContainsErrorSymbol == right.ContainsErrorSymbol
            && left.BaseAndInterfaceIds.SequenceEqual(right.BaseAndInterfaceIds)
            && left.Attributes.SequenceEqual(right.Attributes)
            && left.RelevantTypeReferences.SequenceEqual(right.RelevantTypeReferences)
            && SemanticClaimsEqual(left.Semantics, right.Semantics),
        (ComponentFact left, ComponentFact right) =>
            left.ComponentId == right.ComponentId
            && left.ComponentKind == right.ComponentKind
            && left.ProjectIds.SequenceEqual(right.ProjectIds),
        (RelationFact left, RelationFact right) =>
            left.RelationId == right.RelationId
            && left.SourceId == right.SourceId
            && left.TargetId == right.TargetId
            && left.Partition == right.Partition
            && left.RelationKind == right.RelationKind
            && left.UnresolvedReason == right.UnresolvedReason
            && left.Details.SequenceEqual(right.Details),
        _ => false,
    };

    private static bool SemanticClaimsEqual(SymbolSemanticDetails? left, SymbolSemanticDetails? right) =>
        (left, right) switch
        {
            (null, null) => true,
            ({ } first, { } second) =>
                first.ImplementedMemberIds.SequenceEqual(second.ImplementedMemberIds)
                && first.OverriddenMemberId == second.OverriddenMemberId,
            _ => false,
        };

    private static bool ProjectEvaluationEqual(ProjectEvaluationDetails? left, ProjectEvaluationDetails? right) =>
        (left, right) switch
        {
            (null, null) => true,
            ({ } first, { } second) =>
                first.DeclaredSdk == second.DeclaredSdk
                && first.EvaluatedImports.SequenceEqual(second.EvaluatedImports)
                && first.TargetFrameworks.SequenceEqual(second.TargetFrameworks)
                && first.RequestedAnalysis == second.RequestedAnalysis
                && first.EffectiveResolution == second.EffectiveResolution
                && first.RestorePerformed == second.RestorePerformed
                && first.Isolation == second.Isolation,
            _ => false,
        };

    private static bool TargetEvaluationEqual(TargetEvaluationDetails? left, TargetEvaluationDetails? right) =>
        (left, right) switch
        {
            (null, null) => true,
            ({ } first, { } second) =>
                first.OutputType == second.OutputType
                && first.AssemblyName == second.AssemblyName
                && first.RootNamespace == second.RootNamespace
                && first.CompileItems.SequenceEqual(second.CompileItems)
                && first.ProjectReferences.SequenceEqual(second.ProjectReferences)
                && first.PackageReferences.SequenceEqual(second.PackageReferences)
                && first.References.SequenceEqual(second.References)
                && first.Constants.SequenceEqual(second.Constants)
                && first.LanguageVersion == second.LanguageVersion
                && first.NullableMode == second.NullableMode
                && first.CompiledExtensions.SequenceEqual(second.CompiledExtensions),
            _ => false,
        };

    private static int ResolutionRank(FactResolution resolution) => resolution switch
    {
        FactResolution.NotApplicable => 0,
        FactResolution.Unresolved => 1,
        FactResolution.Candidate => 2,
        FactResolution.Heuristic => 3,
        FactResolution.Syntactic => 4,
        FactResolution.Partial => 5,
        FactResolution.Exact => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unknown fact resolution."),
    };

    private static IFact WithHeader(IFact fact, FactHeader header) => fact switch
    {
        SolutionFact value => value with { Header = header },
        ProjectFact value => value with { Header = header },
        TargetFact value => value with { Header = header },
        DocumentFact value => value with { Header = header },
        SourceSectionFact value => value with { Header = header },
        SymbolFact value => value with { Header = header },
        ComponentFact value => value with { Header = header },
        RelationFact value => value with { Header = header },
        _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.GetType(), "Unsupported factual type."),
    };

    private static AnalysisDiagnostic Conflict(FactId scopeId, string code, string message) =>
        AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Error,
            DiagnosticStage.Validation,
            scopeId,
            message,
            [new DiagnosticData("rule", "merge-conflict")]);
}
