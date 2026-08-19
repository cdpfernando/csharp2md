using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed record DetectorCoverageInput(
    FactId ScopeId,
    FactLevel FactLevel,
    DetectorId DetectorId,
    CoverageApplicability Applicability,
    CoverageAttempt Attempt,
    FactResolution Resolution,
    ImmutableArray<DiagnosticId> DiagnosticIds);

internal sealed record ScopeCoverageInput(
    FactId ScopeId,
    CoverageApplicability Applicability,
    CoverageAttempt Attempt);

internal sealed record CoverageProjectionRequest(
    AnalysisMode RequestedAnalysis,
    ImmutableArray<IFact> Facts,
    ImmutableArray<AnalysisDiagnostic> Diagnostics,
    ImmutableArray<DetectorCoverageInput> DetectorScopes,
    ImmutableArray<ScopeCoverageInput> ScopeOverrides = default);

internal sealed record CoverageProjectionSummary(
    int TotalScopes,
    int DiagnosticCount,
    int ExactScopes,
    int PartialScopes,
    int SyntacticScopes,
    int UnresolvedScopes,
    int NotApplicableScopes,
    int UnattemptedScopes,
    int DegradedScopes);

internal sealed record CoverageProjectionResult(
    ImmutableArray<AnalysisDiagnosticJson> Diagnostics,
    ImmutableArray<CoverageFact> Coverage,
    CoverageProjectionSummary Summary)
{
    public static CoverageProjectionResult Empty { get; } = new(
        [],
        [],
        new CoverageProjectionSummary(0, 0, 0, 0, 0, 0, 0, 0, 0));
}

internal static class CoverageProjector
{
    public static CoverageProjectionResult Project(CoverageProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = request.Diagnostics
            .GroupBy(static diagnostic => diagnostic.Id)
            .Select(static group => group.Order().First())
            .Order()
            .ToImmutableArray();
        var diagnosticIdsByScope = diagnostics
            .GroupBy(static diagnostic => diagnostic.ScopeId)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static diagnostic => diagnostic.Id).OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray());
        var scopeOverrides = (request.ScopeOverrides.IsDefault ? [] : request.ScopeOverrides)
            .ToDictionary(static entry => entry.ScopeId);
        var coverage = ImmutableArray.CreateBuilder<CoverageFact>();

        foreach (var fact in request.Facts
                     .Where(static fact => fact is SolutionFact or ProjectFact or TargetFact or DocumentFact)
                     .OrderBy(static fact => fact.Header.Id.Value, StringComparer.Ordinal))
        {
            var level = fact switch
            {
                SolutionFact => FactLevel.Solution,
                ProjectFact => FactLevel.Project,
                TargetFact => FactLevel.Target,
                DocumentFact => FactLevel.Document,
                _ => throw new InvalidOperationException("Unsupported coverage fact level."),
            };
            var diagnosticIds = fact.Header.DiagnosticIds
                .Concat(diagnosticIdsByScope.GetValueOrDefault(fact.Header.Id, []));
            scopeOverrides.TryGetValue(fact.Header.Id, out var scopeOverride);
            var applicability = scopeOverride?.Applicability
                ?? (fact.Header.Resolution is FactResolution.NotApplicable
                    ? CoverageApplicability.NotApplicable
                    : CoverageApplicability.Applicable);
            var attempt = scopeOverride?.Attempt
                ?? (fact.Header.Resolution is FactResolution.NotApplicable
                    ? CoverageAttempt.NotAttempted
                    : request.RequestedAnalysis is AnalysisMode.Semantic
                        ? CoverageAttempt.Attempted
                        : CoverageAttempt.NotAttempted);
            coverage.Add(new CoverageFact(
                fact.Header.Id,
                level,
                null,
                applicability,
                attempt,
                fact.Header.Resolution,
                CanonicalIds(diagnosticIds)));
        }

        foreach (var detector in request.DetectorScopes)
        {
            var diagnosticIds = detector.DiagnosticIds
                .Concat(diagnosticIdsByScope.GetValueOrDefault(detector.ScopeId, []));
            coverage.Add(new CoverageFact(
                detector.ScopeId,
                detector.FactLevel,
                detector.DetectorId,
                detector.Applicability,
                detector.Attempt,
                detector.Resolution,
                CanonicalIds(diagnosticIds)));
        }

        var canonicalCoverage = CanonicalCoverage(coverage);
        var projectedDiagnostics = diagnostics.Select(MapDiagnostic).ToImmutableArray();
        return new CoverageProjectionResult(
            projectedDiagnostics,
            canonicalCoverage,
            Summary(canonicalCoverage, projectedDiagnostics.Length));
    }

    private static ImmutableArray<CoverageFact> CanonicalCoverage(IEnumerable<CoverageFact> coverage)
    {
        var result = ImmutableArray.CreateBuilder<CoverageFact>();
        foreach (var group in coverage.GroupBy(static entry => (entry.ScopeId, entry.FactLevel, entry.DetectorId)))
        {
            var entries = group.ToArray();
            if (entries.Skip(1).Any(entry => !CoverageEqual(entries[0], entry)))
            {
                throw new InvalidOperationException(
                    $"Conflicting coverage entries share scope '{group.Key.ScopeId.Value}', level '{group.Key.FactLevel}', and detector '{group.Key.DetectorId?.Value}'.");
            }

            result.Add(entries[0]);
        }

        return result
            .OrderBy(static entry => entry.ScopeId.Value, StringComparer.Ordinal)
            .ThenBy(static entry => entry.FactLevel)
            .ThenBy(static entry => entry.DetectorId?.Value, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool CoverageEqual(CoverageFact left, CoverageFact right) =>
        left.ScopeId == right.ScopeId
        && left.FactLevel == right.FactLevel
        && left.DetectorId == right.DetectorId
        && left.Applicability == right.Applicability
        && left.Attempt == right.Attempt
        && left.Resolution == right.Resolution
        && left.DiagnosticIds.SequenceEqual(right.DiagnosticIds);

    private static CoverageProjectionSummary Summary(
        ImmutableArray<CoverageFact> coverage,
        int diagnosticCount) => new(
        coverage.Length,
        diagnosticCount,
        coverage.Count(static entry => entry.Resolution is FactResolution.Exact),
        coverage.Count(static entry => entry.Resolution is FactResolution.Partial),
        coverage.Count(static entry => entry.Resolution is FactResolution.Syntactic),
        coverage.Count(static entry => entry.Resolution is FactResolution.Unresolved),
        coverage.Count(static entry => entry.Resolution is FactResolution.NotApplicable),
        coverage.Count(static entry => entry.Attempt is CoverageAttempt.NotAttempted),
        coverage.Count(static entry =>
            entry.Resolution is FactResolution.Partial or FactResolution.Syntactic or FactResolution.Unresolved
            && !entry.DiagnosticIds.IsEmpty));

    private static AnalysisDiagnosticJson MapDiagnostic(AnalysisDiagnostic diagnostic) => new(
        diagnostic.Id.Value,
        diagnostic.Code,
        Wire(diagnostic.Severity),
        Wire(diagnostic.Stage),
        diagnostic.ScopeId.Value,
        SafeMessage(diagnostic.Message),
        diagnostic.Data
            .Where(static item => IsSafeDetail(item.Value))
            .Select(static item => new DiagnosticDataJson(item.Key, item.Value))
            .ToImmutableArray(),
        diagnostic.Evidence.Select(static evidence => new EvidenceJson(
            evidence.DocumentId.Value,
            evidence.RelativePath,
            evidence.StartLine,
            evidence.StartColumn,
            evidence.EndLine,
            evidence.EndColumn)).ToImmutableArray(),
        diagnostic.ExtensionId?.Value);

    private static string SafeMessage(string message) => IsSafeDetail(message)
        ? message
        : "Diagnostic detail was redacted from machine output.";

    private static bool IsSafeDetail(string value)
    {
        if (value.Contains('\r', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains(" at ", StringComparison.Ordinal)
                && value.Contains(" in ", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = token.Trim('\'', '"', '(', ')', '[', ']', ',', ';');
            if (candidate.Length > 0 && candidate[0] == '/'
                || candidate.Length >= 3
                && char.IsAsciiLetter(candidate[0])
                && candidate[1] == ':'
                && candidate[2] is '/' or '\\')
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<DiagnosticId> CanonicalIds(IEnumerable<DiagnosticId> diagnosticIds) =>
        diagnosticIds.Distinct().OrderBy(static id => id.Value, StringComparer.Ordinal).ToImmutableArray();

    private static string Wire<T>(T value) where T : struct, Enum =>
        value.ToString().Replace("NotApplicable", "not-applicable", StringComparison.Ordinal)
            .Replace("NotAttempted", "not-attempted", StringComparison.Ordinal)
            .ToLowerInvariant();
}
