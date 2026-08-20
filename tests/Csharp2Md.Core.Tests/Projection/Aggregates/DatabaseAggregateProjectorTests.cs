using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class DatabaseAggregateProjectorTests
{
    // DAD-19: cross-fragment repetition is expected, and reconciles to one entry keeping every span.
    [Fact]
    public void Project_NodeEmittedByThreeFragments_YieldsOneEntryCarryingAllThreeEvidenceSpans()
    {
        var result = DatabaseAggregateProjector.Project(
        [
            Fragment(Table("tb_order", evidenceLine: 5, path: "Data/OrderConfiguration.cs")),
            Fragment(Table("tb_order", evidenceLine: 9, path: "Data/OrderQueries.cs")),
            Fragment(Table("tb_order", evidenceLine: 12, path: "Data/OrderArchive.cs")),
        ]);

        var node = Assert.Single(result.Objects);
        Assert.Equal("tb_order", node.Name);
        Assert.Equal(
            ["Data/OrderArchive.cs", "Data/OrderConfiguration.cs", "Data/OrderQueries.cs"],
            node.Header.Evidence.Select(evidence => evidence.RelativePath).Order(StringComparer.Ordinal));
    }

    // DAD-19: when two contributions disagree, the more proven resolution is the one that survives.
    [Fact]
    public void Project_ContributionsDisagreeingOnResolution_KeepsTheStrongerOne()
    {
        var result = DatabaseAggregateProjector.Project(
        [
            Fragment(Table("tb_order", resolution: FactResolution.Heuristic)),
            Fragment(Table("tb_order", resolution: FactResolution.Exact, evidenceLine: 9)),
        ]);

        Assert.Equal("exact", Assert.Single(result.Objects).Header.Resolution);
    }

    // AD-014 records identifiers verbatim, so a case-only difference stays two nodes - and is reported.
    [Fact]
    public void Project_ObjectIdsDifferingOnlyByCase_StayTwoEntriesAndReportOneInformationalDiagnostic()
    {
        var result = DatabaseAggregateProjector.Project(
        [
            Fragment(Table("Orders"), Table("orders")),
        ]);

        Assert.Equal(2, result.Objects.Length);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("C2M-DA-002", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Information, diagnostic.Severity);
        Assert.Equal(
            2,
            diagnostic.Data.Count(item => item.Key == "object_id"));
    }

    // Distinct names are not a collision, so nothing is reported.
    [Fact]
    public void Project_DistinctObjectNames_ReportNoDiagnostic()
    {
        var result = DatabaseAggregateProjector.Project([Fragment(Table("Orders"), Table("Invoices"))]);

        Assert.Empty(result.Diagnostics);
    }

    // DAD-20: the catalogue must not depend on the order fragments happened to be produced in.
    [Fact]
    public void Project_FragmentsInEitherOrder_YieldTheSameCanonicalOrdering()
    {
        var first = Fragment(Table("zeta"), Column("zeta", "z_id"));
        var second = Fragment(Table("alpha"), Column("alpha", "a_id"));

        var forward = DatabaseAggregateProjector.Project([first, second]);
        var reversed = DatabaseAggregateProjector.Project([second, first]);

        Assert.Equal(
            forward.Objects.Select(node => node.ObjectId),
            reversed.Objects.Select(node => node.ObjectId));
        Assert.Equal(
            ["alpha", "zeta"],
            forward.Objects.Select(node => node.Name));
        Assert.Equal(["a_id", "z_id"], forward.Columns.Select(column => column.Name));
    }

    // DAD-19 applies to columns on the same terms, and each keeps its owning object.
    [Fact]
    public void Project_ColumnEmittedTwice_YieldsOneEntryStillNamingItsObject()
    {
        var result = DatabaseAggregateProjector.Project(
        [
            Fragment(Column("tb_order", "order_status")),
            Fragment(Column("tb_order", "order_status", evidenceLine: 11)),
        ]);

        var column = Assert.Single(result.Columns);
        Assert.Equal("order_status", column.Name);
        Assert.Equal(ObjectId("tb_order").Value, column.ObjectId);
        Assert.Equal(2, column.Header.Evidence.Length);
    }

    // The catalogue is written through the same mapper the fragments are, so its wire names must be
    // the hyphenated schema forms rather than a lowercased enum name.
    [Fact]
    public void Project_Entries_CarryTheSchemasWireNamesForKindAndResolution()
    {
        var result = DatabaseAggregateProjector.Project([Fragment(Table("tb_order"), Column("tb_order", "order_status"))]);

        Assert.Equal("database-object", Assert.Single(result.Objects).Header.Kind);
        Assert.Equal("database-column", Assert.Single(result.Columns).Header.Kind);
        Assert.Equal("table", Assert.Single(result.Objects).Kind);
        Assert.Equal("exact", Assert.Single(result.Objects).Header.Resolution);
    }

    [Fact]
    public void Project_NoFragments_YieldsAnEmptyCatalogue()
    {
        var result = DatabaseAggregateProjector.Project([]);

        Assert.Empty(result.Objects);
        Assert.Empty(result.Columns);
        Assert.Empty(result.Diagnostics);
    }

    private static ProjectFactId ProjectId { get; } = ProjectFactId.Create("src/App/App.csproj");

    private static ValidatedFactFragment Fragment(params IFact[] facts) => new([.. facts], []);

    private static DatabaseObjectFactId ObjectId(string name) =>
        DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, DatabaseObjectKind.Table, name);

    private static DatabaseObjectFact Table(
        string name,
        FactResolution resolution = FactResolution.Exact,
        int evidenceLine = 5,
        string path = "Data/OrderConfiguration.cs")
    {
        var objectId = ObjectId(name);
        return new DatabaseObjectFact(
            Header(objectId.ToFactId(), FactKind.DatabaseObject, resolution, evidenceLine, path),
            objectId,
            DatabaseObjectFactId.UnknownConnection,
            DatabaseObjectKind.Table,
            name);
    }

    private static DatabaseColumnFact Column(
        string objectName,
        string name,
        int evidenceLine = 7,
        string path = "Data/OrderConfiguration.cs")
    {
        var objectId = ObjectId(objectName);
        var columnId = DatabaseColumnFactId.Create(objectId, name);
        return new DatabaseColumnFact(
            Header(columnId.ToFactId(), FactKind.DatabaseColumn, FactResolution.Exact, evidenceLine, path),
            columnId,
            objectId,
            name);
    }

    private static FactHeader Header(
        FactId id,
        FactKind kind,
        FactResolution resolution,
        int evidenceLine,
        string path) =>
        FactHeader.Create(
            id,
            kind,
            resolution,
            [new FactProvenance("csharp2md.syntax", "1", DetectorId.Create("csharp2md.dataaccess.efcore"), "1.0.0")],
            [new Evidence(DocumentFactId.Create(ProjectId, path), path, evidenceLine, 1, evidenceLine, 40)]);
}
