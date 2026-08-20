using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Projection.Aggregates;

/// <summary>
/// The database node catalogue as it is written: one entry per node id, plus whatever the reconciliation
/// found worth reporting.
/// </summary>
internal sealed record DatabaseProjectionResult(
    ImmutableArray<DatabaseObjectFactJson> Objects,
    ImmutableArray<DatabaseColumnFactJson> Columns,
    ImmutableArray<AnalysisDiagnostic> Diagnostics)
{
    public static DatabaseProjectionResult Empty { get; } = new([], [], []);
}

/// <summary>
/// Produces the database node catalogue from already validated fragments. Cross-fragment repetition of
/// a node is expected rather than a structural failure, because duplicate detection is scoped to one
/// fragment: the same table is named by every document that configures or queries it. Repetitions
/// collapse to one entry whose evidence is the union of theirs and whose resolution is the strongest
/// any of them proved (DAD-19).
/// </summary>
internal static class DatabaseAggregateProjector
{
    /// <summary>
    /// Two object ids differing only by case. AD-014 records identifiers verbatim and never normalizes
    /// them, so a dialect that treats <c>Orders</c> and <c>orders</c> as one table still yields two
    /// nodes here. That is reported rather than silently merged, because merging would need a dialect
    /// this stage deliberately does not know.
    /// </summary>
    private const string CaseCollisionCode = "C2M-DA-002";

    public static DatabaseProjectionResult Project(IEnumerable<ValidatedFactFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        var objects = new Dictionary<DatabaseObjectFactId, DatabaseObjectFact>();
        var columns = new Dictionary<DatabaseColumnFactId, DatabaseColumnFact>();
        foreach (var fragment in fragments)
        {
            ArgumentNullException.ThrowIfNull(fragment);
            foreach (var fact in fragment.Facts)
            {
                switch (fact)
                {
                    case DatabaseObjectFact node:
                        objects[node.ObjectId] = objects.TryGetValue(node.ObjectId, out var priorObject)
                            ? priorObject with { Header = Merge(priorObject.Header, node.Header) }
                            : node;
                        break;

                    case DatabaseColumnFact column:
                        columns[column.ColumnId] = columns.TryGetValue(column.ColumnId, out var priorColumn)
                            ? priorColumn with { Header = Merge(priorColumn.Header, column.Header) }
                            : column;
                        break;
                }
            }
        }

        var orderedObjects = objects.Values
            .OrderBy(static node => node.ObjectId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        return new DatabaseProjectionResult(
            orderedObjects.Select(FactualJsonMapper.MapDatabaseObject).ToImmutableArray(),
            columns.Values
                .OrderBy(static column => column.ColumnId.Value, StringComparer.Ordinal)
                .Select(FactualJsonMapper.MapDatabaseColumn)
                .ToImmutableArray(),
            CaseCollisions(orderedObjects));
    }

    /// <summary>
    /// One header describing both contributions: the union of their evidence and provenance, and the
    /// stronger of their resolutions.
    /// </summary>
    private static FactHeader Merge(FactHeader left, FactHeader right) =>
        FactHeader.Create(
            left.Id,
            left.Kind,
            FactResolutionAlgebra.Stronger(left.Resolution, right.Resolution),
            left.Provenance.Concat(right.Provenance),
            left.Evidence.Concat(right.Evidence),
            left.DiagnosticIds.Concat(right.DiagnosticIds));

    private static ImmutableArray<AnalysisDiagnostic> CaseCollisions(ImmutableArray<DatabaseObjectFact> nodes)
    {
        var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();
        foreach (var collision in nodes
            .GroupBy(static node => node.ObjectId.Value, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Skip(1).Any()))
        {
            var kept = collision.First();
            diagnostics.Add(AnalysisDiagnostic.Create(
                CaseCollisionCode,
                DiagnosticSeverity.Information,
                DiagnosticStage.Projection,
                kept.Header.Id,
                "Database object identities differ only by case; they stay separate nodes.",
                [
                    new DiagnosticData("rule", "case-only-node-collision"),
                    .. collision.Select(static node => new DiagnosticData("object_id", node.ObjectId.Value)),
                ]));
        }

        return diagnostics.Order().ToImmutableArray();
    }
}
