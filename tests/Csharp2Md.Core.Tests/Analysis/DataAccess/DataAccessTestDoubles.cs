using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

/// <summary>
/// An <c>IDataAccessAnalyzer</c> whose behaviour each test supplies, so the seam can be exercised
/// without any real analyzer existing behind it.
/// </summary>
internal sealed class StubDataAccessAnalyzer : IDataAccessAnalyzer
{
    private readonly Action<DataAccessContext, ImmutableArray<RawDatabaseClaim>.Builder> _analyze;

    public StubDataAccessAnalyzer(
        string name,
        Action<DataAccessContext, ImmutableArray<RawDatabaseClaim>.Builder> analyze)
    {
        Id = DataAccessAnalyzerId.Create(name);
        _analyze = analyze;
    }

    public DataAccessAnalyzerId Id { get; }

    public int AnalyzeCount { get; private set; }

    public void Analyze(DataAccessContext context, ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        AnalyzeCount++;
        _analyze(context, claims);
    }

    /// <summary>An analyzer that appends one claim naming itself through the claim's object text.</summary>
    public static StubDataAccessAnalyzer Appending(string name, string objectText) =>
        new(name, (context, claims) => claims.Add(
            DataAccessTestFacts.Claim(context, DataAccessAnalyzerId.Create(name)) with { ObjectText = objectText }));

    /// <summary>An analyzer that appends one claim and then throws, so its partial work must be discarded.</summary>
    public static StubDataAccessAnalyzer AppendingThenThrowing(string name, Exception failure) =>
        new(name, (context, claims) =>
        {
            claims.Add(DataAccessTestFacts.Claim(context, DataAccessAnalyzerId.Create(name)) with
            {
                ObjectText = "partial-work",
            });
            throw failure;
        });
}

internal static class DataAccessTestFacts
{
    public static ProjectFactId ProjectId { get; } = ProjectFactId.Create("src/App/App.csproj");

    public static DataAccessContext Context(
        string source = "class OrderRepository { void Load() { Query(); } }",
        string relativePath = "src/App/OrderRepository.cs",
        string documentName = "OrderRepository.cs")
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var owners = new Dictionary<MemberDeclarationSyntax, SymbolFactId>();
        foreach (var declaration in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (declaration is GlobalStatementSyntax)
            {
                continue;
            }

            owners[declaration] = SymbolFactId.CreateSyntactic(
                ProjectId,
                documentName,
                SyntaxFactExtractor.DeclarationKind(declaration),
                SyntaxFactExtractor.DeclarationSignature(declaration));
        }

        return DataAccessContext.Create(
            DocumentFactId.Create(ProjectId, documentName), relativePath, root, owners);
    }

    public static RawDatabaseClaim Claim(DataAccessContext context, DataAccessAnalyzerId analyzerId) => new()
    {
        Kind = DatabaseClaimKind.Access,
        OwnerId = context.OwnerOf(context.Root),
        Evidence = new Evidence(context.DocumentId, context.RelativePath, 1, 1, 1, 10),
        ShapeConfidence = FactResolution.Syntactic,
        AnalyzerId = analyzerId,
    };
}
