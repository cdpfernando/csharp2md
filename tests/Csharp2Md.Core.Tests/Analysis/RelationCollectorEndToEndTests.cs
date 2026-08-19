using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// T16: the single-place, literal restatement of spec.md's P1 and P2 Independent Tests, run against
/// the real <c>fixtures/SyntheticSolution</c> fixture in the default (syntax-only) mode - the exact
/// scenario the originally-reported defect (<c>relations: []</c>) described. Every assertion here maps
/// 1:1 to spec.md's own Independent Test wording, closing the feature's Requirement Traceability.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RelationCollectorEndToEndTests(RelationCollectorEndToEndFixture fixture)
    : IClassFixture<RelationCollectorEndToEndFixture>
{
    [Fact]
    public void P1_PaymentsServiceDocument_IncludesSubscribeHandlePublishRelations_WithTargetTextSetAndTargetIdNull()
    {
        var payments = fixture.FindDocumentFragment("PaymentsService.cs");

        var subscribes = Assert.Single(payments.Relations, relation => relation.RelationKind == "subscribes");
        AssertTargetText(subscribes, "OrderPlaced");
        Assert.Null(subscribes.TargetId);

        var handles = Assert.Single(payments.Relations, relation => relation.RelationKind == "handles");
        AssertTargetText(handles, "OrderPlaced");
        Assert.Null(handles.TargetId);

        var publishes = Assert.Single(payments.Relations, relation => relation.RelationKind == "publishes");
        AssertTargetText(publishes, "PaymentProcessed");
        Assert.Null(publishes.TargetId);
    }

    [Fact]
    public void P1_OrderServiceDocument_IncludesHttpClientAndHttpCallRelations_WithTargetTextSetAndTargetIdNull()
    {
        var orders = fixture.FindDocumentFragment("OrderService.cs");

        var httpClient = Assert.Single(orders.Relations, relation =>
            relation.RelationKind == "http-client"
            && relation.Details!.Value.Any(detail => detail is { Key: "target_text", Value: "PaymentService" }));
        Assert.Null(httpClient.TargetId);

        var httpCall = Assert.Single(orders.Relations, relation =>
            relation.RelationKind == "http-call"
            && relation.Details!.Value.Any(detail => detail is { Key: "route", Value: "payments/authorize" }));
        Assert.Contains(httpCall.Details!.Value, detail => detail is { Key: "http_method", Value: "POST" });
        Assert.Null(httpCall.TargetId);
    }

    [Fact]
    public void P1_RelationsAreNoLongerEmptyForTheseDocuments_ClosingTheReportedRelationsEmptyArrayDefect()
    {
        var payments = fixture.FindDocumentFragment("PaymentsService.cs");
        var orders = fixture.FindDocumentFragment("OrderService.cs");

        Assert.NotEmpty(payments.Relations);
        Assert.NotEmpty(orders.Relations);
    }

    [Fact]
    public void P2_OrderServiceDocument_ProducesAtLeastOneCallsRelationForThePaymentsClientAuthorizePaymentInvocation()
    {
        var orders = fixture.FindDocumentFragment("OrderService.cs");

        Assert.Contains(orders.Relations, relation =>
            relation.RelationKind == "calls"
            && relation.Details!.Value.Any(detail =>
                detail.Key == "target_text" && detail.Value == "paymentsClient.AuthorizePayment"));
    }

    [Fact]
    public void P2_PaymentsServiceDocument_ProducesInheritsRelationWithTargetTextPaymentsBase()
    {
        var payments = fixture.FindDocumentFragment("PaymentsService.cs");

        var inherits = Assert.Single(payments.Relations, relation => relation.RelationKind == "inherits");
        Assert.Contains(inherits.Details!.Value, detail =>
            detail.Key == "target_text" && detail.Value is "PaymentsBase" or "Payments.PaymentsBase");
    }

    private static void AssertTargetText(RelationFactJson relation, string expected) =>
        Assert.Contains(relation.Details!.Value, detail => detail is { Key: "target_text" } && detail.Value == expected);
}

public sealed class RelationCollectorEndToEndFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-relc-e2e-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-relc-e2e-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(manifest, Output, topic: "acme-relation-e2e", domain: "system-design").Request);
        var result = await new AnalysisEngine().AnalyzeAsync(request);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
        }
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(Output))
        {
            Directory.Delete(Output, recursive: true);
        }

        return Task.CompletedTask;
    }

    public FactualJsonDocument FindDocumentFragment(string fileName)
    {
        var documentRoot = Path.Combine(TopicLayout.RawRoot(Output), "facts", "document");
        foreach (var path in Directory.EnumerateFiles(documentRoot, "*.json", SearchOption.AllDirectories))
        {
            var candidate = FactualJsonSerializer.Deserialize(File.ReadAllBytes(path));
            if (candidate.Documents.Length == 1
                && candidate.Documents[0].RelativePath.EndsWith(fileName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"No persisted document fragment found for {fileName}.");
    }
}
