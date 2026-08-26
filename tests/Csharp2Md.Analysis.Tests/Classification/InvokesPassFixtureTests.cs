using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class InvokesPassFixtureTests
{
    [Fact]
    [Trait("Requirement", "CLLF-17")]
    public async Task AnalyzeAsync_AcmeOrders_AuthorizeViaPaymentClientAsyncInvokesPaymentClientAuthorize()
    {
        var (_, publication) = await AnalyzeAcmeOrdersWithInvokesAsync();
        var invokes = ReadInvokes(publication);

        Assert.Contains(
            invokes,
            relation => relation.Kind == "invokes"
                && relation.Source.Id.Contains("AuthorizeViaPaymentClientAsync", StringComparison.Ordinal)
                && relation.Source.Id.Contains("OrderService", StringComparison.Ordinal)
                && relation.Target.Id.Contains("PaymentClient", StringComparison.Ordinal)
                && relation.Target.Id.Contains("Authorize", StringComparison.Ordinal)
                && !relation.Target.Id.Contains("AuthorizeViaPaymentClientAsync", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "CLLF-15")]
    [Trait("Requirement", "CLLF-16")]
    public async Task AnalyzeAsync_AcmeOrders_ReceiverShapesEachInvokePaymentClientAuthorizeOnce()
    {
        var (_, publication) = await AnalyzeAcmeOrdersWithInvokesAsync();
        var invokes = ReadInvokes(publication)
            .Where(relation =>
                relation.Kind == "invokes"
                && relation.Source.Id.Contains("ReceiverShapes", StringComparison.Ordinal)
                && relation.Target.Id.Contains("PaymentClient", StringComparison.Ordinal)
                && relation.Target.Id.Contains("Authorize", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(invokes, relation => relation.Source.Id.Contains("ViaField", StringComparison.Ordinal));
        Assert.Contains(invokes, relation => relation.Source.Id.Contains("ViaProperty", StringComparison.Ordinal));
        Assert.Contains(invokes, relation => relation.Source.Id.Contains("ViaPatternVariable", StringComparison.Ordinal));
        Assert.Contains(invokes, relation => relation.Source.Id.Contains(".ctor", StringComparison.Ordinal));
        Assert.Equal(invokes.Length, invokes.Select(relation => relation.Source.Id + "\u001f" + relation.Target.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("Requirement", "CLLF-20")]
    public async Task AnalyzeAsync_AcmeOrders_DoesNotConfirmInvokesIntoBclOrEmitDiagnosticsForFrameworkCalls()
    {
        var (_, publication) = await AnalyzeAcmeOrdersWithInvokesAsync();
        var invokes = ReadInvokes(publication);

        Assert.DoesNotContain(
            invokes,
            relation => relation.Target.Id.Contains("global%3A%3ASystem.", StringComparison.Ordinal)
                || relation.Target.Id.Contains("global::System.", StringComparison.Ordinal)
                || relation.Target.Id.Contains("global%3A%3AMicrosoft.", StringComparison.Ordinal));

        var diagnosticsFragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == "diagnostics.json");
        if (diagnosticsFragment is not null)
        {
            var diagnostics = CanonicalJson.Read<DiagnosticsEnvelope>(diagnosticsFragment.Payload.AsSpan());
            Assert.DoesNotContain(
                diagnostics.Records,
                record => record.Code.Contains("invokes", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static async Task<(SolutionOutcome Outcome, CommittedPublication Publication)> AnalyzeAcmeOrdersWithInvokesAsync()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var stages = PipelineStages.CreateDefault().SetItem(
            3,
            new ClassificationAndPromotionStage(
            [
                new ComponentPass(),
                new EntryPointPass(),
                new BoundaryPass(),
                new ContractPass(),
                new RelationPass(),
                new InvokesPass(),
            ]));
        var result = await new AnalysisEngine(store, stages).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return (outcome, publication);
    }

    private static ImmutableArray<ConfirmedRelationDto> ReadInvokes(CommittedPublication publication)
    {
        var fragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == "relations/confirmed/invokes.json");
        Assert.True(fragment is not null, "Expected relations/confirmed/invokes.json in the publication.");
        return CanonicalJson.Read<ImmutableArray<ConfirmedRelationDto>>(fragment!.Payload.AsSpan());
    }
}
