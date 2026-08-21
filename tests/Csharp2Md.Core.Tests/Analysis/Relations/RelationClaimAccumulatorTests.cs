using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations;

public sealed class RelationClaimAccumulatorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");

    // The memory bound: a silent document costs nothing, even when other documents in the same run
    // do contribute.
    [Fact]
    public void Add_DocumentThatContributesNoClaims_RetainsNoExtentForIt()
    {
        var accumulator = new RelationClaimAccumulator();
        var silent = DocumentFactId.Create(ProjectId, "Silent.cs");
        var talkative = DocumentFactId.Create(ProjectId, "OrderService.cs");

        accumulator.Add(silent, "src/App/Silent.cs", [10, 20, 30], []);
        accumulator.Add(
            talkative,
            "src/App/OrderService.cs",
            [10, 20, 30],
            [Claim(talkative, "src/App/OrderService.cs", 1, "paymentsClient.Authorize")]);

        var snapshot = accumulator.ToSnapshot();

        var extent = Assert.Single(snapshot.Documents);
        Assert.Equal(talkative, extent.DocumentId);
        Assert.DoesNotContain(silent, snapshot.Documents.Select(static document => document.DocumentId));
    }

    [Fact]
    public void Add_DocumentThatContributesClaims_RetainsItsExtentWithTheDocumentsLineLengths()
    {
        var accumulator = new RelationClaimAccumulator();
        var documentId = DocumentFactId.Create(ProjectId, "OrderService.cs");

        accumulator.Add(
            documentId,
            "src/App/OrderService.cs",
            [11, 22, 33],
            [Claim(documentId, "src/App/OrderService.cs", 2, "paymentsClient.Authorize")]);

        var extent = Assert.Single(accumulator.ToSnapshot().Documents);
        Assert.Equal("src/App/OrderService.cs", extent.RelativePath);
        Assert.Equal<int>([11, 22, 33], extent.LineLengths);
    }

    // RELR-21: the order documents were walked in must not reach the snapshot.
    [Fact]
    public void ToSnapshot_ClaimOrder_IsIdenticalRegardlessOfTheOrderDocumentsWereAdded()
    {
        var forwards = Fill(addAlphaFirst: true).ToSnapshot();
        var backwards = Fill(addAlphaFirst: false).ToSnapshot();

        Assert.Equal(
            ["Alpha.One", "Alpha.Two", "Omega.One"],
            forwards.Claims.Select(static claim => claim.TargetText));
        Assert.Equal(
            forwards.Claims.Select(static claim => claim.TargetText),
            backwards.Claims.Select(static claim => claim.TargetText));
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

    // AddResolved: the database resolver's already-targeted claims carry no extent of their own.
    [Fact]
    public void AddResolved_AcceptsAlreadyTargetedClaims_WithoutRecordingAnExtent()
    {
        var accumulator = new RelationClaimAccumulator();
        var documentId = DocumentFactId.Create(ProjectId, "OrderRepository.cs");
        var targetId = SymbolFactId.CreateSyntactic(ProjectId, "TbOrder.cs", "table", "table tb_order").ToFactId();
        var resolved = new RawRelation
        {
            Kind = "accesses",
            OwnerId = documentId.ToFactId(),
            Evidence = new Evidence(documentId, "src/App/OrderRepository.cs", 5, 1, 5, 10),
            ShapeConfidence = FactResolution.Exact,
            Partition = RelationPartition.Data,
            Details = [new RelationDetail("target_text", "tb_order")],
            TargetId = targetId,
            ProducerMethod = ResolutionMethod.Configured,
        };

        accumulator.AddResolved([resolved]);
        var snapshot = accumulator.ToSnapshot();

        var claim = Assert.Single(snapshot.Claims);
        Assert.Equal(targetId, claim.TargetId);
        Assert.Equal(ResolutionMethod.Configured, claim.ProducerMethod);
        Assert.Empty(snapshot.Documents);
    }

    [Fact]
    public void AddResolved_WithNoClaims_DoesNothing()
    {
        var accumulator = new RelationClaimAccumulator();

        accumulator.AddResolved([]);

        Assert.Empty(accumulator.ToSnapshot().Claims);
    }

    private static RelationClaimAccumulator Fill(bool addAlphaFirst)
    {
        var accumulator = new RelationClaimAccumulator();
        var alpha = DocumentFactId.Create(ProjectId, "Alpha.cs");
        var omega = DocumentFactId.Create(ProjectId, "Omega.cs");

        void AddAlpha() => accumulator.Add(
            alpha,
            "src/App/Alpha.cs",
            [40, 40, 40],
            [
                Claim(alpha, "src/App/Alpha.cs", 1, "Alpha.One"),
                Claim(alpha, "src/App/Alpha.cs", 2, "Alpha.Two"),
            ]);

        void AddOmega() => accumulator.Add(
            omega,
            "src/App/Omega.cs",
            [40, 40, 40],
            [Claim(omega, "src/App/Omega.cs", 1, "Omega.One")]);

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

    private static RawRelation Claim(
        DocumentFactId documentId, string relativePath, int line, string targetText) => new()
        {
            Kind = "calls",
            OwnerId = documentId.ToFactId(),
            Evidence = new Evidence(documentId, relativePath, line, 1, line, 20),
            ShapeConfidence = FactResolution.Syntactic,
            Partition = RelationPartition.Structural,
            Details = [new RelationDetail("target_text", targetText)],
            TargetText = targetText,
        };
}
