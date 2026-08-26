using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Domain.Facets;

namespace Csharp2Md.Analysis.Tests.Extraction;

public sealed class SqlStatementReaderTests
{
    [Fact]
    [Trait("Requirement", "PK-19")]
    public void TryRead_SelectWithWhere_ReadsOperationTargetAndColumns()
    {
        var read = SqlStatementReader.TryRead("SELECT Id, Status FROM Orders WHERE Id = @id", out var facts);

        Assert.True(read);
        Assert.Equal(DataOperationKind.Read, facts.Operation);
        Assert.Equal("Orders", facts.Target);
        Assert.True(facts.Columns.AsSpan().SequenceEqual(["Id", "Status"]));
    }

    [Fact]
    [Trait("Requirement", "PK-27")]
    public void TryRead_SelectStar_YieldsZeroColumns()
    {
        var read = SqlStatementReader.TryRead("SELECT * FROM Orders", out var facts);

        Assert.True(read);
        Assert.Equal(DataOperationKind.Read, facts.Operation);
        Assert.Equal("Orders", facts.Target);
        Assert.Empty(facts.Columns);
    }

    [Fact]
    public void TryRead_Insert_ReadsOperationTargetAndColumnList()
    {
        var read = SqlStatementReader.TryRead(
            "INSERT INTO Orders (Id, Status, Amount) VALUES (@id, @status, @amount)",
            out var facts);

        Assert.True(read);
        Assert.Equal(DataOperationKind.Insert, facts.Operation);
        Assert.Equal("Orders", facts.Target);
        Assert.True(facts.Columns.AsSpan().SequenceEqual(["Id", "Status", "Amount"]));
    }

    [Fact]
    public void TryRead_Update_ReadsOperationTargetAndAssignedNames()
    {
        var read = SqlStatementReader.TryRead("UPDATE Orders SET Status = @status WHERE Id = @id", out var facts);

        Assert.True(read);
        Assert.Equal(DataOperationKind.Update, facts.Operation);
        Assert.Equal("Orders", facts.Target);
        Assert.True(facts.Columns.AsSpan().SequenceEqual(["Status"]));
    }

    [Fact]
    [Trait("Requirement", "PK-19")]
    public void TryRead_DeleteWithBracketedTarget_UnquotesToBareIdentifier()
    {
        var read = SqlStatementReader.TryRead("DELETE FROM [Orders] WHERE Status = 'Archived'", out var facts);

        Assert.True(read);
        Assert.Equal(DataOperationKind.Delete, facts.Operation);
        Assert.Equal("Orders", facts.Target);
        Assert.Empty(facts.Columns);
    }

    [Fact]
    [Trait("Requirement", "PK-19")]
    public void TryRead_DoubleQuotedTarget_UnquotesToBareIdentifier()
    {
        var read = SqlStatementReader.TryRead("DELETE FROM \"Orders\"", out var facts);

        Assert.True(read);
        Assert.Equal("Orders", facts.Target);
    }

    [Theory]
    [InlineData("EXEC usp_RebuildOrderTotals")]
    [InlineData("EXECUTE usp_RebuildOrderTotals")]
    [InlineData("exec usp_RebuildOrderTotals")]
    public void TryRead_Exec_ReadsOperationAndProcedureName(string statement)
    {
        var read = SqlStatementReader.TryRead(statement, out var facts);

        Assert.True(read);
        Assert.Equal(DataOperationKind.Execute, facts.Operation);
        Assert.Equal("usp_RebuildOrderTotals", facts.Target);
        Assert.Empty(facts.Columns);
    }

    [Fact]
    public void TryRead_LowercaseKeywords_StillParsesAndPreservesIdentifierCasing()
    {
        var read = SqlStatementReader.TryRead("select id from Orders", out var facts);

        Assert.True(read);
        Assert.Equal(DataOperationKind.Read, facts.Operation);
        Assert.Equal("Orders", facts.Target);
        Assert.True(facts.Columns.AsSpan().SequenceEqual(["id"]));
    }

    [Fact]
    [Trait("Requirement", "PK-39")]
    public void TryRead_InterpolationHoleAsTarget_IsRejected()
    {
        var read = SqlStatementReader.TryRead("SELECT * FROM {tableName}", out _);

        Assert.False(read);
    }

    [Fact]
    [Trait("Requirement", "PK-39")]
    public void TryRead_ParameterMarkerAsTarget_IsRejected()
    {
        var read = SqlStatementReader.TryRead("SELECT Id FROM @table", out _);

        Assert.False(read);
    }

    [Fact]
    [Trait("Requirement", "PK-39")]
    public void TryRead_TargetWithEmbeddedWhitespace_IsRejected()
    {
        var read = SqlStatementReader.TryRead("UPDATE Order Two SET Status = @status", out _);

        Assert.False(read);
    }

    [Fact]
    [Trait("Requirement", "PK-40")]
    public void TryRead_LeadingKeywordOutsideClosedSet_IsRejected()
    {
        var read = SqlStatementReader.TryRead("MERGE INTO Orders USING Staging", out _);

        Assert.False(read);
    }

    [Fact]
    public void TryRead_SelectWithoutFrom_IsRejected()
    {
        var read = SqlStatementReader.TryRead("SELECT 1", out _);

        Assert.False(read);
    }

    [Fact]
    public void TryRead_InsertWithoutColumnList_IsRejected()
    {
        var read = SqlStatementReader.TryRead("INSERT INTO Orders VALUES (@id)", out _);

        Assert.False(read);
    }
}
