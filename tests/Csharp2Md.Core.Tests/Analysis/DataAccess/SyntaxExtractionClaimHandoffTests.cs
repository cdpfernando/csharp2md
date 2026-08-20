using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

/// <summary>
/// The pass-one handoff: the collector runs inside the syntax walk, where the enclosing-member owner
/// map is live, and its claims leave the extractor on <c>SyntaxFactExtraction</c>.
/// </summary>
public sealed class SyntaxExtractionClaimHandoffTests
{
    private const string Source = """
        class OrderRepository
        {
            void Load()
            {
                Query();
            }
        }
        """;

    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");

    // The owner map is the reason pass one lives in the extractor: a claim raised on a node inside a
    // member is owned by that member's symbol id, not by the document.
    [Fact]
    public void Extract_ClaimRaisedInsideAMember_IsOwnedByThatMembersSymbolId()
    {
        var extraction = SyntaxFactExtractor.Extract(
            ProjectId, "src/App/OrderRepository.cs", Source, [OwnerClaimingAnalyzer<InvocationExpressionSyntax>()]);

        var claim = Assert.Single(extraction.DatabaseClaims);
        var expected = extraction.Symbols.Single(static symbol => symbol.SymbolKind == "method").SymbolId.ToFactId();
        Assert.Equal(expected, claim.OwnerId);
        Assert.NotEqual(extraction.Document.DocumentId.ToFactId(), claim.OwnerId);
    }

    // The fallback is reserved for code with no enclosing member.
    [Fact]
    public void Extract_ClaimRaisedOutsideEveryMember_FallsBackToTheDocumentId()
    {
        var extraction = SyntaxFactExtractor.Extract(
            ProjectId, "src/App/Top.cs", "Query();", [OwnerClaimingAnalyzer<InvocationExpressionSyntax>()]);

        var claim = Assert.Single(extraction.DatabaseClaims);
        Assert.Equal(extraction.Document.DocumentId.ToFactId(), claim.OwnerId);
    }

    // The claim's evidence names the document the extractor was asked about.
    [Fact]
    public void Extract_Claim_CarriesTheDocumentsIdentityAndPathAsEvidence()
    {
        var extraction = SyntaxFactExtractor.Extract(
            ProjectId, "src/App/OrderRepository.cs", Source, [OwnerClaimingAnalyzer<InvocationExpressionSyntax>()]);

        var claim = Assert.Single(extraction.DatabaseClaims);
        Assert.Equal(extraction.Document.DocumentId, claim.Evidence.DocumentId);
        Assert.Equal("src/App/OrderRepository.cs", claim.Evidence.RelativePath);
    }

    // DAD-18 through the extractor: an analyzer that throws leaves a diagnostic on the extraction and
    // does not fail the walk.
    [Fact]
    public void Extract_WhenAnAnalyzerThrows_ReportsC2MDA001AndStillReturnsTheExtraction()
    {
        var failing = StubDataAccessAnalyzer.AppendingThenThrowing(
            "csharp2md.dataaccess.failing", new InvalidOperationException("boom"));

        var extraction = SyntaxFactExtractor.Extract(
            ProjectId, "src/App/OrderRepository.cs", Source, [failing]);

        Assert.Equal("C2M-DA-001", Assert.Single(extraction.DatabaseDiagnostics).Code);
        Assert.Empty(extraction.DatabaseClaims);
        Assert.Contains(extraction.Symbols, static symbol => symbol.Name == "OrderRepository");
    }

    // With nothing registered the extractor produces exactly what it produced before, claims aside.
    [Fact]
    public void Extract_WithNoAnalyzersRegistered_LeavesTheExtractionOtherwiseUnchanged()
    {
        var withoutAnalyzers = SyntaxFactExtractor.Extract(ProjectId, "src/App/OrderRepository.cs", Source);
        var withAnalyzer = SyntaxFactExtractor.Extract(
            ProjectId, "src/App/OrderRepository.cs", Source, [OwnerClaimingAnalyzer<InvocationExpressionSyntax>()]);

        Assert.Empty(withoutAnalyzers.DatabaseClaims);
        Assert.Empty(withoutAnalyzers.DatabaseDiagnostics);
        Assert.Equal(withAnalyzer.Document.DocumentId, withoutAnalyzers.Document.DocumentId);
        Assert.Equal(
            withAnalyzer.Document.Sections.Select(static section => section.Header.Id.Value),
            withoutAnalyzers.Document.Sections.Select(static section => section.Header.Id.Value));
        Assert.Equal(
            withAnalyzer.Document.SymbolIds.Select(static id => id.Value),
            withoutAnalyzers.Document.SymbolIds.Select(static id => id.Value));
        Assert.Equal(
            withAnalyzer.Symbols.Select(static symbol => symbol.SymbolId.Value),
            withoutAnalyzers.Symbols.Select(static symbol => symbol.SymbolId.Value));
        Assert.Equal<SyntacticRelationCandidate>(
            withAnalyzer.RelationCandidates, withoutAnalyzers.RelationCandidates);
        Assert.Equal(
            withAnalyzer.XmlProse.Keys.Select(static id => id.Value).Order(StringComparer.Ordinal),
            withoutAnalyzers.XmlProse.Keys.Select(static id => id.Value).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// An analyzer that claims the first node of the given kind and records whatever owner the context
    /// resolves for it - the smallest probe that can tell a member owner from the document fallback.
    /// </summary>
    private static StubDataAccessAnalyzer OwnerClaimingAnalyzer<TNode>() where TNode : SyntaxNode =>
        new("csharp2md.dataaccess.probe", (context, claims) =>
        {
            var node = context.Root.DescendantNodes().OfType<TNode>().First();
            var span = node.SyntaxTree.GetLineSpan(node.Span);
            claims.Add(new RawDatabaseClaim
            {
                Kind = DatabaseClaimKind.Access,
                OwnerId = context.OwnerOf(node),
                Evidence = new Evidence(
                    context.DocumentId,
                    context.RelativePath,
                    span.StartLinePosition.Line + 1,
                    span.StartLinePosition.Character + 1,
                    span.EndLinePosition.Line + 1,
                    span.EndLinePosition.Character + 1),
                ShapeConfidence = FactResolution.Syntactic,
                AnalyzerId = DataAccessAnalyzerId.Create("csharp2md.dataaccess.probe"),
            });
        });
}
