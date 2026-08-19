using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// T12: proves <c>RelationCollector.CreateFacts</c> is actually wired into the live
/// <see cref="AnalysisEngine"/> pipeline in the default, syntax-only analysis mode - spec.md's P1
/// and P2 Independent Tests against the real <c>fixtures/SyntheticSolution</c> fixture, where the
/// reported defect was <c>relations: []</c> for these exact documents.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RelationCollectorWiringTests(RelationCollectorWiringFixture fixture)
    : IClassFixture<RelationCollectorWiringFixture>
{
    [Fact]
    public void AnalyzeAsync_SyntaxOnlyMode_PaymentsServiceDocument_EmitsSubscribeHandlePublishAndInheritsRelations()
    {
        var document = fixture.FindDocumentFragment("PaymentsService.cs");
        var relations = document.Relations;

        Assert.Contains(relations, relation =>
            relation.RelationKind == "subscribes"
            && HasDetail(relation, "target_text", "OrderPlaced"));
        Assert.Contains(relations, relation =>
            relation.RelationKind == "handles"
            && HasDetail(relation, "target_text", "OrderPlaced"));
        Assert.Contains(relations, relation =>
            relation.RelationKind == "publishes"
            && HasDetail(relation, "target_text", "PaymentProcessed"));
        Assert.Contains(relations, relation =>
            relation.RelationKind == "inherits"
            && relation.Details!.Value.Any(detail =>
                detail.Key == "target_text" && detail.Value is "PaymentsBase" or "Payments.PaymentsBase"));
    }

    [Fact]
    public void AnalyzeAsync_SyntaxOnlyMode_OrderServiceDocument_EmitsHttpClientHttpCallAndCallsRelations()
    {
        var document = fixture.FindDocumentFragment("OrderService.cs");
        var relations = document.Relations;

        Assert.Contains(relations, relation =>
            relation.RelationKind == "http-client"
            && HasDetail(relation, "target_text", "PaymentService"));
        Assert.Contains(relations, relation => relation.RelationKind == "http-call");
        Assert.Contains(relations, relation =>
            relation.RelationKind == "calls"
            && relation.Details!.Value.Any(detail =>
                detail.Key == "target_text" && detail.Value.Contains("AuthorizePayment", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnalyzeAsync_SyntaxOnlyMode_EveryEmittedRelation_HasNullTargetIdAndUnresolvedReason()
    {
        var document = fixture.FindDocumentFragment("PaymentsService.cs");

        Assert.NotEmpty(document.Relations);
        Assert.All(document.Relations, relation =>
        {
            Assert.Null(relation.TargetId);
            Assert.False(string.IsNullOrWhiteSpace(relation.UnresolvedReason));
        });
    }

    private static bool HasDetail(RelationFactJson relation, string key, string value) =>
        relation.Details!.Value.Any(detail => detail.Key == key && detail.Value == value);
}

public sealed class RelationCollectorWiringFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-relc-wiring-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-relc-wiring-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(manifest, Output, topic: "acme-relation-wiring", domain: "system-design").Request);
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
