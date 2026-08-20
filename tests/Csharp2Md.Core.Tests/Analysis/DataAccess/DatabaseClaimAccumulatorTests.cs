using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class DatabaseClaimAccumulatorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DataAccessAnalyzerId AnalyzerId =
        DataAccessAnalyzerId.Create("csharp2md.dataaccess.efcore");

    // The memory bound: a silent document costs nothing, even when other documents in the same run
    // do contribute.
    [Fact]
    public void Add_DocumentThatContributesNoClaims_RetainsNoExtentForIt()
    {
        var accumulator = new DatabaseClaimAccumulator();
        var silent = DocumentFactId.Create(ProjectId, "Silent.cs");
        var talkative = DocumentFactId.Create(ProjectId, "OrderRepository.cs");

        accumulator.Add(silent, "src/App/Silent.cs", [10, 20, 30], []);
        accumulator.Add(
            talkative,
            "src/App/OrderRepository.cs",
            [10, 20, 30],
            [Claim(talkative, "src/App/OrderRepository.cs", 1, "tb_order")]);

        var snapshot = accumulator.ToSnapshot();

        var extent = Assert.Single(snapshot.Documents);
        Assert.Equal(talkative, extent.DocumentId);
        Assert.DoesNotContain(silent, snapshot.Documents.Select(static document => document.DocumentId));
    }

    [Fact]
    public void Add_DocumentThatContributesClaims_RetainsItsExtentWithTheDocumentsLineLengths()
    {
        var accumulator = new DatabaseClaimAccumulator();
        var documentId = DocumentFactId.Create(ProjectId, "OrderRepository.cs");

        accumulator.Add(
            documentId,
            "src/App/OrderRepository.cs",
            [11, 22, 33],
            [Claim(documentId, "src/App/OrderRepository.cs", 2, "tb_order")]);

        var extent = Assert.Single(accumulator.ToSnapshot().Documents);
        Assert.Equal("src/App/OrderRepository.cs", extent.RelativePath);
        Assert.Equal<int>([11, 22, 33], extent.LineLengths);
    }

    // DAD-20: the order documents were walked in must not reach the snapshot.
    [Fact]
    public void ToSnapshot_ClaimOrder_IsIdenticalRegardlessOfTheOrderDocumentsWereAdded()
    {
        var forwards = Fill(addAlphaFirst: true).ToSnapshot();
        var backwards = Fill(addAlphaFirst: false).ToSnapshot();

        Assert.Equal(
            ["tb_alpha_one", "tb_alpha_two", "tb_omega"],
            forwards.Claims.Select(static claim => claim.ObjectText));
        Assert.Equal(
            forwards.Claims.Select(static claim => claim.ObjectText),
            backwards.Claims.Select(static claim => claim.ObjectText));
    }

    [Fact]
    public void ToSnapshot_ExtentOrder_IsIdenticalRegardlessOfTheOrderDocumentsWereAdded()
    {
        var forwards = Fill(addAlphaFirst: true).ToSnapshot();
        var backwards = Fill(addAlphaFirst: false).ToSnapshot();

        var expected = forwards.Documents
            .Select(static document => document.DocumentId.Value)
            .ToArray();
        Assert.Equal(expected.OrderBy(static value => value, StringComparer.Ordinal), expected);
        Assert.Equal(expected, backwards.Documents.Select(static document => document.DocumentId.Value));
    }

    private static DatabaseClaimAccumulator Fill(bool addAlphaFirst)
    {
        var accumulator = new DatabaseClaimAccumulator();
        var alpha = DocumentFactId.Create(ProjectId, "Alpha.cs");
        var omega = DocumentFactId.Create(ProjectId, "Omega.cs");

        void AddAlpha() => accumulator.Add(
            alpha,
            "src/App/Alpha.cs",
            [40, 40, 40],
            [
                Claim(alpha, "src/App/Alpha.cs", 1, "tb_alpha_one"),
                Claim(alpha, "src/App/Alpha.cs", 2, "tb_alpha_two"),
            ]);

        void AddOmega() => accumulator.Add(
            omega,
            "src/App/Omega.cs",
            [40, 40, 40],
            [Claim(omega, "src/App/Omega.cs", 1, "tb_omega")]);

        if (addAlphaFirst)
        {
            AddAlpha();
            AddOmega();
        }
        else
        {
            AddOmega();
            AddAlpha();
        }

        return accumulator;
    }

    private static RawDatabaseClaim Claim(
        DocumentFactId documentId, string relativePath, int line, string objectText) => new()
        {
            Kind = DatabaseClaimKind.Access,
            OwnerId = documentId.ToFactId(),
            Evidence = new Evidence(documentId, relativePath, line, 1, line, 20),
            ShapeConfidence = FactResolution.Syntactic,
            AnalyzerId = AnalyzerId,
            ObjectText = objectText,
        };
}
