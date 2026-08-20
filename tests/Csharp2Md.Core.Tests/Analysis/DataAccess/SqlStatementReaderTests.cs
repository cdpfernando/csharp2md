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
