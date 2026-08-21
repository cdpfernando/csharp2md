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
/// Turns pass two's resolved database objects and columns into facts and runs them through the
/// identical validate path every document fragment takes. A failure here is a structural failure with
/// the same diagnostics and the same meaning it would have on a document - no new failure semantics are
/// introduced. Database relations (RELR-32) are no longer this builder's concern - they leave
/// <c>DatabaseMappingResolver.Resolve</c> as
/// <see cref="Csharp2Md.Core.Analysis.Relations.RawRelation"/> claims for the
/// <c>RelationClaimAccumulator</c> instead.
/// </summary>
internal static class DatabaseFragmentBuilder
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

        // RELR-32: database relations are now RawRelation claims (resolution.Relations) handed to the
        // RelationClaimAccumulator instead of being minted into facts here - RelationResolver is the
        // only writer of RelationFact (AD-018). A database object's or column's identity references
        // nothing outside this fragment, so no knownFactIds are needed once relations leave it.
        var validation = validate(FactValidationInput.Create(
            facts.ToImmutable(),
            documents: resolution.Documents));

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
}
