using System.Reflection;
using System.Runtime.CompilerServices;
using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class DataAccessContractsTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "OrderRepository.cs");
    private static readonly DataAccessAnalyzerId AnalyzerId =
        DataAccessAnalyzerId.Create("csharp2md.dataaccess.efcore");

    // Design.md's Data Models section lists these five as required members of the raw claim.
    [Theory]
    [InlineData(nameof(RawDatabaseClaim.Kind))]
    [InlineData(nameof(RawDatabaseClaim.OwnerId))]
    [InlineData(nameof(RawDatabaseClaim.Evidence))]
    [InlineData(nameof(RawDatabaseClaim.ShapeConfidence))]
    [InlineData(nameof(RawDatabaseClaim.AnalyzerId))]
    public void RawDatabaseClaim_RequiredMember_IsEnforcedByTheCompiler(string memberName)
    {
        var member = typeof(RawDatabaseClaim).GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(member);
        Assert.NotNull(member.GetCustomAttribute<RequiredMemberAttribute>());
    }

    [Fact]
    public void RawDatabaseClaim_CarriesEveryFieldFromTheDesign()
    {
        var evidence = EvidenceAt(12, 5, 12, 40);

        var claim = new RawDatabaseClaim
        {
            Kind = DatabaseClaimKind.ColumnAccess,
            OwnerId = OwnerId,
            Evidence = evidence,
            ShapeConfidence = FactResolution.Heuristic,
            AnalyzerId = AnalyzerId,
            EntityText = "Order",
            PropertyText = "Status",
            ObjectText = "tb_order",
            ObjectKind = DatabaseObjectKind.Table,
            ColumnText = "order_status",
            Operation = DatabaseOperation.Update,
            Usage = ColumnUsage.Write,
            SqlText = "UPDATE tb_order SET order_status = @status",
            UnresolvedReason = "receiver-not-proven",
        };

        Assert.Equal(DatabaseClaimKind.ColumnAccess, claim.Kind);
        Assert.Equal(OwnerId, claim.OwnerId);
        Assert.Equal(evidence, claim.Evidence);
        Assert.Equal(FactResolution.Heuristic, claim.ShapeConfidence);
        Assert.Equal(AnalyzerId, claim.AnalyzerId);
        Assert.Equal("Order", claim.EntityText);
        Assert.Equal("Status", claim.PropertyText);
        Assert.Equal("tb_order", claim.ObjectText);
        Assert.Equal(DatabaseObjectKind.Table, claim.ObjectKind);
        Assert.Equal("order_status", claim.ColumnText);
        Assert.Equal(DatabaseOperation.Update, claim.Operation);
        Assert.Equal(ColumnUsage.Write, claim.Usage);
        Assert.Equal("UPDATE tb_order SET order_status = @status", claim.SqlText);
        Assert.Equal("receiver-not-proven", claim.UnresolvedReason);
    }

    // DAD-16: at most 2000 characters, enforced where the value is set rather than where it is written.
    [Fact]
    public void RawDatabaseClaim_SqlTextLongerThanTheCap_IsTruncatedAtConstruction()
    {
        var sql = "SELECT " + new string('a', 5000);

        var claim = Claim() with { SqlText = sql };

        Assert.Equal(2000, claim.SqlText!.Length);
        Assert.Equal(sql[..2000], claim.SqlText);
    }

    [Fact]
    public void RawDatabaseClaim_SqlTextAtTheCap_IsPreservedWhole()
    {
        var sql = new string('b', 2000);

        var claim = Claim() with { SqlText = sql };

        Assert.Equal(sql, claim.SqlText);
    }

    // DAD-13: every claim carries evidence, so an unset one cannot be constructed.
    [Fact]
    public void RawDatabaseClaim_WithoutEvidence_IsRejectedAtConstruction()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RawDatabaseClaim
        {
            Kind = DatabaseClaimKind.Access,
            OwnerId = OwnerId,
            Evidence = default,
            ShapeConfidence = FactResolution.Syntactic,
            AnalyzerId = AnalyzerId,
        });

        Assert.Equal("evidence", exception.ParamName);
    }

    // DAD-17: classification comes only from observation, so an unset field never reads as an
    // observed read.
    [Fact]
    public void RawDatabaseClaim_WithOnlyRequiredMembers_ClassifiesNothing()
    {
        var claim = Claim();

        Assert.Equal(DatabaseOperation.Unknown, claim.Operation);
        Assert.Equal(ColumnUsage.Unknown, claim.Usage);
        Assert.Null(claim.EntityText);
        Assert.Null(claim.PropertyText);
        Assert.Null(claim.ObjectText);
        Assert.Null(claim.ObjectKind);
        Assert.Null(claim.ColumnText);
        Assert.Null(claim.SqlText);
        Assert.Null(claim.UnresolvedReason);
    }

    [Fact]
    public void DataAccessAnalyzerId_Create_YieldsTheDetectorIdentityOfThatName()
    {
        var analyzerId = DataAccessAnalyzerId.Create("csharp2md.dataaccess.sql");

        Assert.Equal(DetectorId.Create("csharp2md.dataaccess.sql").Value, analyzerId.ToDetectorId().Value);
        Assert.Equal(analyzerId.ToDetectorId().Value, analyzerId.Value);
    }

    [Fact]
    public void DataAccessAnalyzerId_Create_RejectsANameThatIsNotReverseDns()
    {
        Assert.Throws<ArgumentException>(static () => DataAccessAnalyzerId.Create("Not A Detector"));
    }

    // T15's owner rule at unit level: the enclosing member owns the claim.
    [Fact]
    public void OwnerOf_NodeInsideAMember_ReturnsThatMembersSymbolId()
    {
        var context = ContextFor("class OrderRepository { void Load() { Query(); } }", out var root);
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        var owner = context.OwnerOf(invocation);

        Assert.Equal(
            SymbolFactId.CreateSyntactic(
                ProjectId,
                "OrderRepository.cs",
                SyntaxFactExtractor.DeclarationKind(method),
                SyntaxFactExtractor.DeclarationSignature(method)).ToFactId(),
            owner);
    }

    [Fact]
    public void OwnerOf_NodeWithNoEnclosingMember_FallsBackToTheDocumentId()
    {
        var context = ContextFor("Query();", out var root);
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var owner = context.OwnerOf(invocation);

        Assert.Equal(DocumentId.ToFactId(), owner);
    }

    private static FactId OwnerId =>
        SymbolFactId.CreateSyntactic(ProjectId, "OrderRepository.cs", "class", "class OrderRepository").ToFactId();

    private static RawDatabaseClaim Claim() => new()
    {
        Kind = DatabaseClaimKind.Access,
        OwnerId = OwnerId,
        Evidence = EvidenceAt(3, 1, 3, 20),
        ShapeConfidence = FactResolution.Syntactic,
        AnalyzerId = AnalyzerId,
    };

    private static Evidence EvidenceAt(int startLine, int startColumn, int endLine, int endColumn) =>
        new(DocumentId, "src/App/OrderRepository.cs", startLine, startColumn, endLine, endColumn);

    /// <summary>
    /// Builds the owner map the way the syntax extractor does - one entry per declared member, global
    /// statements excluded - so the fallback rule runs against a realistic map, not an empty one.
    /// </summary>
    private static DataAccessContext ContextFor(string source, out SyntaxNode root)
    {
        root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var owners = new Dictionary<MemberDeclarationSyntax, SymbolFactId>();
        foreach (var declaration in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (declaration is GlobalStatementSyntax)
            {
                continue;
            }

            owners[declaration] = SymbolFactId.CreateSyntactic(
                ProjectId,
                "OrderRepository.cs",
                SyntaxFactExtractor.DeclarationKind(declaration),
                SyntaxFactExtractor.DeclarationSignature(declaration));
        }

        return DataAccessContext.Create(DocumentId, "src/App/OrderRepository.cs", root, owners);
    }
}
