using Csharp2Md.Core.Analysis.DataAccess.Sql;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class SqlStatementReaderTests
{
    // DAD-21: every recognised verb, compared case-insensitively, yields the operation it derives.
    [Theory]
    [InlineData("SELECT Id FROM Orders", DatabaseOperation.Read)]
    [InlineData("select Id from Orders", DatabaseOperation.Read)]
    [InlineData("INSERT INTO Orders (Id) VALUES (1)", DatabaseOperation.Insert)]
    [InlineData("InSeRt InTo Orders (Id) VALUES (1)", DatabaseOperation.Insert)]
    [InlineData("UPDATE Orders SET Status = 'Paid'", DatabaseOperation.Update)]
    [InlineData("update Orders set Status = 'Paid'", DatabaseOperation.Update)]
    [InlineData("DELETE FROM Orders", DatabaseOperation.Delete)]
    [InlineData("delete from Orders", DatabaseOperation.Delete)]
    [InlineData("MERGE INTO Orders USING Staging ON 1 = 1", DatabaseOperation.Update)]
    [InlineData("merge into Orders using Staging on 1 = 1", DatabaseOperation.Update)]
    [InlineData("EXEC usp_GetOrder", DatabaseOperation.Execute)]
    [InlineData("exec usp_GetOrder", DatabaseOperation.Execute)]
    [InlineData("CALL usp_GetOrder()", DatabaseOperation.Execute)]
    [InlineData("call usp_GetOrder()", DatabaseOperation.Execute)]
    public void TryRead_RecognisedVerb_YieldsTheOperationThatVerbDerives(
        string sql, DatabaseOperation expected)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Equal(expected, statement.Operation);
    }

    // DAD-21: the verb is the first *significant* token, so leading whitespace does not hide it.
    [Fact]
    public void TryRead_StatementBehindLeadingWhitespace_StillReadsTheVerbAndTarget()
    {
        Assert.True(SqlStatementReader.TryRead("\r\n\t   SELECT Id FROM Orders", out var statement));

        Assert.Equal(DatabaseOperation.Read, statement.Operation);
        Assert.Equal("Orders", statement.Target);
    }

    // DAD-21: a leading `--` comment line is skipped rather than read as the first token.
    [Fact]
    public void TryRead_StatementBehindLeadingLineComment_StillReadsTheVerbAndTarget()
    {
        Assert.True(SqlStatementReader.TryRead(
            "-- fetch the order\nSELECT Id FROM Orders", out var statement));

        Assert.Equal(DatabaseOperation.Read, statement.Operation);
        Assert.Equal("Orders", statement.Target);
    }

    // DAD-22: the target identifier that follows each verb's anchor keyword.
    [Theory]
    [InlineData("SELECT Id, Status FROM Orders WHERE Id = @id", "Orders")]
    [InlineData("INSERT INTO Orders (Id) VALUES (1)", "Orders")]
    [InlineData("UPDATE Orders SET Status = 'Paid'", "Orders")]
    [InlineData("DELETE FROM Orders WHERE Id = @id", "Orders")]
    [InlineData("MERGE INTO Orders USING Staging ON 1 = 1", "Orders")]
    [InlineData("EXEC usp_GetOrder", "usp_GetOrder")]
    [InlineData("CALL usp_GetOrder()", "usp_GetOrder")]
    public void TryRead_PlainIdentifierTarget_ReadsThatIdentifier(string sql, string expected)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Equal(expected, statement.Target);
    }

    // AD-014: a qualified name is recorded verbatim, never normalized or stripped.
    [Fact]
    public void TryRead_SchemaQualifiedTarget_RecordsTheQualifiedNameVerbatim()
    {
        Assert.True(SqlStatementReader.TryRead("SELECT Id FROM dbo.Orders", out var statement));

        Assert.Equal("dbo.Orders", statement.Target);
    }

    // DAD-23: EXEC and CALL prove the target is a procedure.
    [Theory]
    [InlineData("EXEC usp_GetOrder")]
    [InlineData("CALL usp_GetOrder()")]
    public void TryRead_ExecOrCallVerb_ClassifiesTheTargetAsProcedure(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Equal(DatabaseObjectKind.Procedure, statement.ObjectKind);
    }

    // DAD-23: every other recognised verb proves an access but not the target's nature.
    [Theory]
    [InlineData("SELECT Id FROM Orders")]
    [InlineData("INSERT INTO Orders (Id) VALUES (1)")]
    [InlineData("UPDATE Orders SET Status = 'Paid'")]
    [InlineData("DELETE FROM Orders")]
    [InlineData("MERGE INTO Orders USING Staging ON 1 = 1")]
    public void TryRead_NonProcedureVerb_LeavesTheTargetKindUnknown(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Equal(DatabaseObjectKind.Unknown, statement.ObjectKind);
    }

    // DAD-22 / DAD-28: a target the reader cannot read as a plain identifier is left unread, never guessed.
    [Theory]
    [InlineData("SELECT Id FROM [Orders]")]
    [InlineData("SELECT Id FROM \"Orders\"")]
    [InlineData("SELECT Id FROM (SELECT 1)")]
    [InlineData("SELECT 1")]
    [InlineData("DELETE Orders")]
    [InlineData("MERGE Orders USING Staging ON 1 = 1")]
    [InlineData("EXEC @statement")]
    [InlineData("UPDATE Orders WHERE Id = @id")]
    public void TryRead_UnreadableTarget_RecognisesTheVerbButReadsNoTarget(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Null(statement.Target);
    }

    // DAD-24: the INSERT column list yields exactly the listed columns, verbatim and in order.
    [Fact]
    public void TryRead_InsertWithAColumnList_YieldsExactlyThoseColumns()
    {
        Assert.True(SqlStatementReader.TryRead(
            "INSERT INTO Orders (Id, Status, Amount) VALUES (@id, @status, @amount)",
            out var statement));

        Assert.Equal(["Id", "Status", "Amount"], statement.WrittenColumns.ToArray());
    }

    // DAD-25: the SET assignment list yields exactly the assigned columns - not the assigned values,
    // and not the columns the WHERE clause compares.
    [Fact]
    public void TryRead_UpdateWithASetList_YieldsTheAssignedColumnsOnly()
    {
        Assert.True(SqlStatementReader.TryRead(
            "UPDATE Orders SET Status = 'Paid', Amount = 10 WHERE Id = @id", out var statement));

        Assert.Equal(["Status", "Amount"], statement.WrittenColumns.ToArray());
    }

    // DAD-25: a value expression containing parentheses and commas does not leak into the column list.
    [Fact]
    public void TryRead_UpdateAssigningAFunctionResult_StillYieldsTheAssignedColumnsOnly()
    {
        Assert.True(SqlStatementReader.TryRead(
            "UPDATE Orders SET Status = COALESCE(@status, 'New'), Amount = 10", out var statement));

        Assert.Equal(["Status", "Amount"], statement.WrittenColumns.ToArray());
    }

    // T24: a malformed or unclosed list yields no columns rather than a partial guess.
    [Theory]
    [InlineData("INSERT INTO Orders (Id, Status VALUES (1, 2)")]
    [InlineData("INSERT INTO Orders (Id, Status")]
    [InlineData("INSERT INTO Orders (Id, UPPER(Status)) VALUES (1, 2)")]
    [InlineData("INSERT INTO Orders (Id, [Status]) VALUES (1, 2)")]
    public void TryRead_MalformedInsertColumnList_YieldsNoColumns(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Empty(statement.WrittenColumns);
    }

    // T24: statements that write no column list prove no written columns.
    [Theory]
    [InlineData("INSERT INTO Orders VALUES (1, 2)")]
    [InlineData("SELECT Id FROM Orders")]
    [InlineData("DELETE FROM Orders WHERE Id = @id")]
    [InlineData("UPDATE Orders WHERE Id = @id")]
    public void TryRead_StatementWithoutAWrittenColumnList_YieldsNoColumns(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Empty(statement.WrittenColumns);
    }

    // DAD-26: columns compared against a parameter or a literal in a WHERE clause.
    [Fact]
    public void TryRead_WhereComparingColumnsToAParameterAndALiteral_YieldsBothColumns()
    {
        Assert.True(SqlStatementReader.TryRead(
            "SELECT Id FROM Orders WHERE Id = @id AND Status = 'Paid'", out var statement));

        Assert.Equal(["Id", "Status"], statement.FilterColumns.ToArray());
    }

    // DAD-26: the WHERE clause of a write statement is read on the same terms.
    [Fact]
    public void TryRead_DeleteWithAWhereClause_YieldsTheFilterColumn()
    {
        Assert.True(SqlStatementReader.TryRead(
            "DELETE FROM Orders WHERE Id = @id", out var statement));

        Assert.Equal(["Id"], statement.FilterColumns.ToArray());
    }

    // T25: an alias-qualified reference yields the column's own name; the alias is not part of it.
    [Fact]
    public void TryRead_AliasQualifiedFilterColumn_YieldsTheColumnNameWithoutTheAlias()
    {
        Assert.True(SqlStatementReader.TryRead(
            "SELECT o.Id FROM Orders o WHERE o.Id = @id", out var statement));

        Assert.Equal(["Id"], statement.FilterColumns.ToArray());
    }

    // DAD-26: every comparison operator the reader recognises proves the same filter.
    [Theory]
    [InlineData("SELECT Id FROM Orders WHERE Amount = 10")]
    [InlineData("SELECT Id FROM Orders WHERE Amount <> 10")]
    [InlineData("SELECT Id FROM Orders WHERE Amount != 10")]
    [InlineData("SELECT Id FROM Orders WHERE Amount < 10")]
    [InlineData("SELECT Id FROM Orders WHERE Amount > 10")]
    [InlineData("SELECT Id FROM Orders WHERE Amount <= 10")]
    [InlineData("SELECT Id FROM Orders WHERE Amount >= 10")]
    public void TryRead_ComparisonOperator_YieldsTheComparedColumn(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Equal(["Amount"], statement.FilterColumns.ToArray());
    }

    // DAD-26: one entry per filtered column, however many times the clause compares it.
    [Fact]
    public void TryRead_ColumnFilteredTwice_YieldsThatColumnOnce()
    {
        Assert.True(SqlStatementReader.TryRead(
            "SELECT Id FROM Orders WHERE Id = @first OR Id = @second", out var statement));

        Assert.Equal(["Id"], statement.FilterColumns.ToArray());
    }

    // T25: a WHERE clause the reader cannot parse yields no columns rather than a guess.
    [Theory]
    [InlineData("SELECT Id FROM Orders WHERE EXISTS (SELECT 1 FROM Payments)")]
    [InlineData("SELECT Id FROM Orders WHERE (Amount + Tax) > 10")]
    [InlineData("SELECT Id FROM Orders WHERE Status = Total")]
    [InlineData("SELECT Id FROM Orders WHERE [Status] = 'Paid'")]
    public void TryRead_UnreadableWhereClause_YieldsNoFilterColumns(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Empty(statement.FilterColumns);
    }

    // T25: a statement with no WHERE clause proves no filter columns.
    [Theory]
    [InlineData("SELECT Id FROM Orders")]
    [InlineData("INSERT INTO Orders (Id) VALUES (1)")]
    [InlineData("EXEC usp_GetOrder")]
    public void TryRead_StatementWithoutAWhereClause_YieldsNoFilterColumns(string sql)
    {
        Assert.True(SqlStatementReader.TryRead(sql, out var statement));

        Assert.Empty(statement.FilterColumns);
    }

    // T23: the reader keys off the first significant token only, so a sentence merely containing a
    // SQL verb is not a statement (spec Edge Case: a verb used as a message).
    [Theory]
    [InlineData("Failed to update the order")]
    [InlineData("Could not delete from the cache")]
    [InlineData("Order {0} was inserted")]
    [InlineData("hello world")]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    [InlineData("-- only a comment")]
    [InlineData("Server=localhost;Database=Orders;User Id=sa;Password=hunter2")]
    public void TryRead_TextThatDoesNotOpenWithARecognisedVerb_ReturnsFalse(string text)
    {
        Assert.False(SqlStatementReader.TryRead(text, out var statement));

        Assert.Equal(default, statement);
    }
}
