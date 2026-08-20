using System.Text.RegularExpressions;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Analysis.DataAccess;

/// <summary>
/// What building the persistence fragment produced: the validated fragment, or nothing plus the
/// validator's own diagnostics. A run with no persistence facts yields neither.
/// </summary>
internal sealed record DatabaseFragmentResult(
    ValidatedFactFragment? Fragment,
    ImmutableArray<AnalysisDiagnostic> Diagnostics)
{
    public static DatabaseFragmentResult Empty { get; } = new(null, []);
}

/// <summary>
/// Turns pass two's resolution into facts and runs them through the identical validate path every
/// document fragment takes. A failure here is a structural failure with the same diagnostics and the
/// same meaning it would have on a document - no new failure semantics are introduced.
/// </summary>
internal static partial class DatabaseFragmentBuilder
{
    private const string EngineId = "csharp2md.syntax";
    private const string EngineVersion = "1";
    private const string AnalyzerVersion = "1.0.0";

    public static DatabaseFragmentResult Build(DatabaseResolution resolution, FragmentValidationFunc validate)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(validate);

        if (resolution.IsEmpty)
        {
            return DatabaseFragmentResult.Empty;
        }

        var facts = ImmutableArray.CreateBuilder<IFact>();
        facts.AddRange(resolution.Objects.Select(Fact));
        facts.AddRange(resolution.Columns.Select(Fact));
        facts.AddRange(RelationFacts(resolution.Relations));

        var validation = validate(FactValidationInput.Create(
            facts.ToImmutable(),
            documents: resolution.Documents,
            // Relation sources are symbols and documents that live in the fragments already persisted
            // for their own documents; the solution-level fragment references them without redeclaring.
            knownFactIds: resolution.Relations.Select(static relation => relation.SourceId).Distinct()));

        return new DatabaseFragmentResult(validation.Fragment, validation.ValidationDiagnostics);
    }

    private static DatabaseObjectFact Fact(ResolvedDatabaseObject node) =>
        new(Header(node.ObjectId.ToFactId(), FactKind.DatabaseObject, node.Resolution, node.AnalyzerIds, node.Evidence),
            node.ObjectId,
            DatabaseObjectFactId.UnknownConnection,
            node.Kind,
            node.Name);

    private static DatabaseColumnFact Fact(ResolvedDatabaseColumn column) =>
        new(Header(column.ColumnId.ToFactId(), FactKind.DatabaseColumn, column.Resolution, column.AnalyzerIds, column.Evidence),
            column.ColumnId,
            column.ObjectId,
            column.Name);

    /// <summary>
    /// One relation fact per resolved relation, identified the way every other relation in the pipeline
    /// is: owner, kind, a fingerprint of the observed details, and a one-based occurrence ordinal that
    /// separates two otherwise identical observations in the same member.
    /// </summary>
    private static IEnumerable<RelationFact> RelationFacts(ImmutableArray<ResolvedDatabaseRelation> relations)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var relation in relations)
        {
            var claim = Fingerprint(relation.Details);
            var key = $"{relation.RelationKind}\0{relation.SourceId.Value}\0{claim}";
            var ordinal = ordinals.GetValueOrDefault(key) + 1;
            ordinals[key] = ordinal;

            var id = RelationFactId.Create(relation.SourceId, relation.RelationKind, claim, ordinal);
            yield return new RelationFact(
                Header(id.ToFactId(), FactKind.Relation, relation.Resolution, [relation.AnalyzerId], [relation.Evidence]),
                id,
                relation.SourceId,
                relation.TargetId,
                RelationPartition.Data,
                relation.RelationKind,
                relation.UnresolvedReason,
                relation.Details.Distinct().Order().ToImmutableArray());
        }
    }

    private static FactHeader Header(
        FactId id,
        FactKind kind,
        FactResolution resolution,
        ImmutableArray<DataAccessAnalyzerId> analyzerIds,
        ImmutableArray<Evidence> evidence) =>
        FactHeader.Create(
            id,
            kind,
            resolution,
            analyzerIds.Select(static analyzerId =>
                new FactProvenance(EngineId, EngineVersion, analyzerId.ToDetectorId(), AnalyzerVersion)),
            evidence);

    /// <summary>
    /// The observed details as one canonical single-line value, which is what the identity grammar
    /// accepts. Preserved SQL text can carry newlines and runs of spaces, so it is collapsed here rather
    /// than trusted.
    /// </summary>
    private static string Fingerprint(ImmutableArray<RelationDetail> details) =>
        WhitespaceRun().Replace(
            string.Join('|', details.Order().Select(static detail => $"{detail.Key}={detail.Value}")),
            " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
