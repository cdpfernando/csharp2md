using Csharp2Md.Core.Facts.Metadata;

namespace Csharp2Md.Core.Analysis.DataAccess;

/// <summary>
/// What one document's collection produced: the surviving claims, plus a diagnostic for every analyzer
/// that failed on it.
/// </summary>
internal sealed record DataAccessCollection(
    ImmutableArray<RawDatabaseClaim> Claims,
    ImmutableArray<AnalysisDiagnostic> Diagnostics)
{
    public static DataAccessCollection Empty { get; } = new([], []);
}

/// <summary>
/// Runs the registered analyzers over one document in a fixed canonical order. An analyzer that throws
/// becomes a <c>C2M-DA-001</c> warning and loses its own partial work; it never fails the document, the
/// project or the run (DAD-18).
/// </summary>
internal static class DataAccessCollector
{
    private const string FailureCode = "C2M-DA-001";

    /// <summary>
    /// The analyzers every collection runs. Phase 3 and Phase 4 register the EF Core and SQL analyzers
    /// here; until then a collection is a no-op and the pipeline behaves exactly as before.
    /// </summary>
    public static ImmutableArray<IDataAccessAnalyzer> RegisteredAnalyzers { get; } = Canonicalize([]);

    public static DataAccessCollection Collect(DataAccessContext context) =>
        Collect(context, RegisteredAnalyzers);

    public static DataAccessCollection Collect(
        DataAccessContext context,
        IEnumerable<IDataAccessAnalyzer> analyzers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(analyzers);

        var canonical = Canonicalize(analyzers);
        if (canonical.IsEmpty)
        {
            return DataAccessCollection.Empty;
        }

        var claims = ImmutableArray.CreateBuilder<RawDatabaseClaim>();
        var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        foreach (var analyzer in canonical)
        {
            // A private builder per analyzer: a failure discards that analyzer's partial claims
            // without touching what the analyzers before it contributed.
            var analyzerClaims = ImmutableArray.CreateBuilder<RawDatabaseClaim>();
            try
            {
                analyzer.Analyze(context, analyzerClaims);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(FailureDiagnostic(context, analyzer, exception));
                continue;
            }

            claims.AddRange(analyzerClaims);
        }

        return new DataAccessCollection(
            claims.ToImmutable(),
            diagnostics.Order().ToImmutableArray());
    }

    private static AnalysisDiagnostic FailureDiagnostic(
        DataAccessContext context,
        IDataAccessAnalyzer analyzer,
        Exception exception) =>
        AnalysisDiagnostic.Create(
            FailureCode,
            DiagnosticSeverity.Warning,
            DiagnosticStage.Detector,
            context.DocumentId.ToFactId(),
            "Data access analyzer failed on this document; its incomplete claims were discarded.",
            [
                new DiagnosticData("analyzer_id", analyzer.Id.Value),
                new DiagnosticData("document_path", context.RelativePath),
                new DiagnosticData("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
            ],
            extensionId: analyzer.Id.ToDetectorId());

    private static ImmutableArray<IDataAccessAnalyzer> Canonicalize(
        IEnumerable<IDataAccessAnalyzer> analyzers) =>
        analyzers
            .OrderBy(static analyzer => analyzer.Id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
}
