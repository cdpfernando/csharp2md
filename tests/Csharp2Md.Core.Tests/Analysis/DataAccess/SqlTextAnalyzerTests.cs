using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.DataAccess.Sql;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class SqlTextAnalyzerTests
{
    // DAD-27: an interpolated statement never invents a destination.
    [Fact]
    public void Analyze_InterpolatedStatement_YieldsAnUnresolvedAccessNamingNoObject()
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = $"SELECT * FROM {tableName}";""")));

        Assert.Equal(DatabaseClaimKind.Access, claim.Kind);
        Assert.Equal(FactResolution.Unresolved, claim.ShapeConfidence);
        Assert.Equal("dynamic-table", claim.ObjectText);
        Assert.Null(claim.ObjectKind);
        Assert.Equal(DatabaseOperation.Read, claim.Operation);
        Assert.Equal("dynamic-sql", claim.UnresolvedReason);
    }

    // DAD-27: a concatenation is unreadable on the same terms as an interpolation.
    [Fact]
    public void Analyze_ConcatenatedStatement_YieldsAnUnresolvedAccessNamingNoObject()
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "DELETE FROM " + tableName;""")));

        Assert.Equal(FactResolution.Unresolved, claim.ShapeConfidence);
        Assert.Equal("dynamic-table", claim.ObjectText);
        Assert.Null(claim.ObjectKind);
        Assert.Equal(DatabaseOperation.Delete, claim.Operation);
        Assert.Equal("dynamic-sql", claim.UnresolvedReason);
    }

    // DAD-27: the unreadable expression is preserved rather than silently discarded.
    [Fact]
    public void Analyze_InterpolatedStatement_PreservesTheExpressionText()
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = $"SELECT * FROM {tableName}";""")));

        Assert.Equal("$\"SELECT * FROM {tableName}\"", claim.SqlText);
    }

    // DAD-28: the verb is readable but the target is not, so the access is unresolved and the
    // statement is kept as evidence.
    [Fact]
    public void Analyze_LiteralWithAnUnreadableTarget_YieldsAnUnresolvedAccessPreservingTheStatement()
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "SELECT Id FROM [Orders]";""")));

        Assert.Equal(FactResolution.Unresolved, claim.ShapeConfidence);
        Assert.Null(claim.ObjectText);
        Assert.Null(claim.ObjectKind);
        Assert.Equal(DatabaseOperation.Read, claim.Operation);
        Assert.Equal("unreadable-sql-target", claim.UnresolvedReason);
        Assert.Equal("SELECT Id FROM [Orders]", claim.SqlText);
    }

    // DAD-16: preserved SQL text is capped at 2000 characters.
    [Fact]
    public void Analyze_StatementLongerThanTheCap_TruncatesThePreservedTextAtTwoThousandCharacters()
    {
        var padding = new string('x', 2100);
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            "var sql = \"SELECT Id FROM [Orders] -- " + padding + "\";")));

        Assert.Equal(2000, claim.SqlText!.Length);
        Assert.StartsWith("SELECT Id FROM [Orders]", claim.SqlText, StringComparison.Ordinal);
    }

    // DAD-15, at the point of capture: a connection string never opens with a SQL verb, so it is never
    // treated as SQL and no credential text can reach a claim.
    [Theory]
    [InlineData("Server=localhost;Database=Orders;User Id=sa;Password=hunter2")]
    [InlineData("Data Source=.;Initial Catalog=Orders;Integrated Security=True")]
    [InlineData("Host=db;Username=app;Password=s3cr3t;Database=orders")]
    public void Analyze_ConnectionStringLiteral_YieldsNoClaims(string connectionString)
    {
        var claims = SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            "var connection = \"" + connectionString + "\";"));

        Assert.Empty(claims);
    }

    // DAD-15: a statement whose text carries a literal credential still yields its access, but the
    // credential text is never preserved as a claim detail.
    [Fact]
    public void Analyze_StatementCarryingALiteralCredential_EmitsTheAccessWithoutPreservingTheText()
    {
        var claims = SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "SELECT Id FROM [Users] WHERE Password = 'hunter2'";"""));

        Assert.Equal(DatabaseClaimKind.Access, claims[0].Kind);
        Assert.Equal(FactResolution.Unresolved, claims[0].ShapeConfidence);
        Assert.All(claims, static claim => Assert.DoesNotContain(
            "hunter2",
            string.Join(
                "|",
                [
                    claim.SqlText, claim.ObjectText, claim.ColumnText,
                    claim.PropertyText, claim.EntityText, claim.UnresolvedReason,
                ]),
            StringComparison.OrdinalIgnoreCase));
    }

    // DAD-15's guard is narrow: a parameterised credential column carries no credential value, so the
    // statement is still preserved.
    [Fact]
    public void Analyze_StatementParameterisingACredentialColumn_StillPreservesTheStatement()
    {
        var claims = SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "UPDATE [Users] SET Password = @password";"""));

        Assert.Equal("UPDATE [Users] SET Password = @password", claims[0].SqlText);
    }

    // spec Edge Case: a verb-leading literal used as a message is still an access, at Syntactic
    // resolution, because syntax cannot prove which receiver it was handed to.
    [Fact]
    public void Analyze_VerbLeadingLiteralUsedAsAMessage_StillYieldsAnAccessAtSyntacticResolution()
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """Console.WriteLine("SELECT Id FROM Orders");""")));

        Assert.Equal(DatabaseClaimKind.Access, claim.Kind);
        Assert.Equal(FactResolution.Syntactic, claim.ShapeConfidence);
        Assert.Equal("Orders", claim.ObjectText);
        Assert.Equal(DatabaseObjectKind.Unknown, claim.ObjectKind);
        Assert.Null(claim.UnresolvedReason);
    }

    // spec Edge Case, the other half: a sentence that merely contains a SQL verb is not a statement.
    [Theory]
    [InlineData("Failed to update the order")]
    [InlineData("Could not delete from the cache")]
    [InlineData("Nothing left to insert into the queue")]
    [InlineData("no persistence api usage here")]
    public void Analyze_SentenceContainingASqlVerb_YieldsNoClaims(string message)
    {
        var claims = SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            "Log(\"" + message + "\");"));

        Assert.Empty(claims);
    }

    // DAD-22 and DAD-23: a readable target is carried as proven object text with the verb's kind, so
    // the resolver can mint a node from it.
    [Theory]
    [InlineData("SELECT Id FROM Orders", "Orders", DatabaseObjectKind.Unknown, DatabaseOperation.Read)]
    [InlineData("EXEC usp_GetOrder", "usp_GetOrder", DatabaseObjectKind.Procedure, DatabaseOperation.Execute)]
    public void Analyze_LiteralWithAReadableTarget_CarriesTheProvenObjectTextAndKind(
        string sql, string expectedObject, DatabaseObjectKind expectedKind, DatabaseOperation expectedOperation)
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            "var sql = \"" + sql + "\";")));

        Assert.Equal(expectedObject, claim.ObjectText);
        Assert.Equal(expectedKind, claim.ObjectKind);
        Assert.Equal(expectedOperation, claim.Operation);
        Assert.Null(claim.SqlText);
    }

    // DAD-24: the INSERT column list becomes one write column claim per listed column.
    [Fact]
    public void Analyze_InsertWithAColumnList_YieldsOneWriteColumnClaimPerColumn()
    {
        var claims = SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "INSERT INTO Orders (Id, Status) VALUES (@id, @status)";"""));

        Assert.Equal(3, claims.Length);
        Assert.Equal(DatabaseOperation.Insert, claims[0].Operation);
        Assert.Equal(["Id", "Status"], claims.Skip(1).Select(static claim => claim.ColumnText));
        Assert.All(claims.Skip(1), static claim =>
        {
            Assert.Equal(DatabaseClaimKind.ColumnAccess, claim.Kind);
            Assert.Equal(ColumnUsage.Write, claim.Usage);
            Assert.Equal("Orders", claim.ObjectText);
        });
    }

    // DAD-25 and DAD-26: assigned columns are writes and compared columns are filters, from one statement.
    [Fact]
    public void Analyze_UpdateWithAWhereClause_SeparatesWrittenColumnsFromFilterColumns()
    {
        var claims = SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "UPDATE Orders SET Status = 'Paid' WHERE Id = @id";"""));

        Assert.Equal(3, claims.Length);
        Assert.Equal(DatabaseOperation.Update, claims[0].Operation);
        Assert.Equal("Status", claims[1].ColumnText);
        Assert.Equal(ColumnUsage.Write, claims[1].Usage);
        Assert.Equal("Id", claims[2].ColumnText);
        Assert.Equal(ColumnUsage.Filter, claims[2].Usage);
    }

    // DAD-13: every claim carries the document's identity, its normalized path, and a source span.
    [Fact]
    public void Analyze_Claim_CarriesTheDocumentIdentityPathAndSpanAsEvidence()
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "SELECT Id FROM Orders";""")));

        Assert.Equal(DataAccessTestFacts.Context().DocumentId, claim.Evidence.DocumentId);
        Assert.Equal("src/App/OrderRepository.cs", claim.Evidence.RelativePath);
        Assert.Equal(5, claim.Evidence.StartLine);
        Assert.Equal(5, claim.Evidence.EndLine);
        Assert.True(claim.Evidence.StartColumn < claim.Evidence.EndColumn);
    }

    // DAD-21: the access is sourced at the enclosing member's symbol id, not at the document.
    [Fact]
    public void Analyze_Access_IsSourcedAtTheEnclosingMembersSymbolId()
    {
        var source = SqlAnalysis.MemberDocument("""var sql = "SELECT Id FROM Orders";""");

        var claim = Assert.Single(SqlAnalysis.Claims(source));

        Assert.Equal(EfCoreAnalysis.SymbolIdOfMethodDeclaration(source, "Run"), claim.OwnerId);
        Assert.NotEqual(DataAccessTestFacts.Context().DocumentId.ToFactId(), claim.OwnerId);
    }

    // Every claim names the analyzer that made it, so its facts carry detector provenance.
    [Fact]
    public void Analyze_Claim_IsAttributedToTheSqlAnalyzer()
    {
        var claim = Assert.Single(SqlAnalysis.Claims(SqlAnalysis.MemberDocument(
            """var sql = "SELECT Id FROM Orders";""")));

        Assert.Equal(DataAccessAnalyzerId.Create("csharp2md.dataaccess.sql"), claim.AnalyzerId);
    }

    // DAD-21 is only reachable on a real run if the analyzer is one the collector actually runs.
    [Fact]
    public void RegisteredAnalyzers_IncludeTheSqlTextAnalyzer()
    {
        var registered = Assert.Single(
            DataAccessCollector.RegisteredAnalyzers.OfType<SqlTextAnalyzer>());

        Assert.Equal(DataAccessAnalyzerId.Create("csharp2md.dataaccess.sql"), registered.Id);
    }
}

internal static class SqlAnalysis
{
    public static ImmutableArray<RawDatabaseClaim> Claims(string source)
    {
        var claims = ImmutableArray.CreateBuilder<RawDatabaseClaim>();
        new SqlTextAnalyzer().Analyze(DataAccessTestFacts.Context(source), claims);
        return claims.ToImmutable();
    }

    /// <summary>A document whose single member evaluates <paramref name="body"/> on line 5.</summary>
    public static string MemberDocument(string body) =>
        $$"""
        class OrderRepository
        {
            public void Run(string tableName)
            {
                {{body}}
            }
        }
        """;
}
