using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-018: reproduces the audit's dropped-invocation regression
/// (OrdersController.GetOrderAsync -> IOrderQueries.GetOrderAsync -> OrderQueries.GetOrderAsync) in
/// the versioned certification corpus. The classifier fix for interface-dispatch disposition lands in
/// a later phase (T23); this task only proves the shape and the current disposition set.
/// </summary>
public sealed class CertificationCorpusInvocationTests
{
    [Fact]
    [Trait("Requirement", "GCPC-018")]
    public async Task AnalyzeAsync_CertificationCorpus_GetOrderStatusHasExactlyThreeInvocationObservations()
    {
        var publication = await AnalyzeCorpusAsync();

        var invocations = ReadShard<ImmutableArray<ObservationDto>>(publication, "observations/invocation.json");
        var actionInvocations = invocations
            .Where(observation => observation.Identity.Owner.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && observation.Identity.Owner.Id.Contains("OrderQueriesController", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, actionInvocations.Length);
    }

    [Fact]
    [Trait("Requirement", "GCPC-018")]
    public async Task AnalyzeAsync_CertificationCorpus_InterfaceAndConcreteImplementationAreInventoriedInDifferentProjects()
    {
        var publication = await AnalyzeCorpusAsync();

        var structural = ReadShard<StructuralFactsShard>(publication, "facts/structural.json");

        var interfaceMethod = Assert.Single(
            structural.Symbols,
            symbol => symbol.OwningProject.Contains("Certification.Api", StringComparison.Ordinal)
                && symbol.CanonicalSymbolSignature.Contains("IOrderQueries", StringComparison.Ordinal)
                && symbol.CanonicalSymbolSignature.Contains("GetOrderStatus", StringComparison.Ordinal));
        var concreteMethod = Assert.Single(
            structural.Symbols,
            symbol => symbol.OwningProject.Contains("Certification.Queries", StringComparison.Ordinal)
                && symbol.CanonicalSymbolSignature.Contains("GetOrderStatus", StringComparison.Ordinal));

        Assert.NotEqual(interfaceMethod.OwningProject, concreteMethod.OwningProject);
        Assert.Contains("Certification.Api", interfaceMethod.OwningProject, StringComparison.Ordinal);
        Assert.Contains("Certification.Queries", concreteMethod.OwningProject, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-018")]
    public async Task AnalyzeAsync_CertificationCorpus_DocumentsTheCurrentPreFixDispositionForTheInterfaceCall()
    {
        var publication = await AnalyzeCorpusAsync();

        var candidates = ReadShard<ImmutableArray<CandidateLinkDto>>(publication, "relations/candidates.json");
        var confirmedInvokes = ReadShard<ImmutableArray<ConfirmedRelationDto>>(
            publication,
            "relations/confirmed/invokes.json");

        // Documented pre-fix baseline (CLLF-07/CLLF-09, GCPC-018): today's InvokesPass already resolves
        // an interface dispatch with a single concrete implementor to one CandidateLink and never a
        // confirmed `invokes` to the interface member. The actual audit-B3 defect this batch reproduces
        // for a later phase (T23) to close is the *framework-call* silence asserted below, and the
        // wider disposition-accounting ledger (GCPC-011..015) that does not exist yet.
        Assert.Contains(
            candidates,
            link => link.Kind == "invokes"
                && link.Source.Id.Contains("OrderQueriesController", StringComparison.Ordinal)
                && link.Source.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && link.ProposedTarget.Id.Contains("Certification.Queries", StringComparison.Ordinal)
                && link.ProposedTarget.Id.Contains("GetOrderStatus", StringComparison.Ordinal));
        Assert.DoesNotContain(
            confirmedInvokes,
            relation => relation.Kind == "invokes"
                && relation.Source.Id.Contains("OrderQueriesController", StringComparison.Ordinal)
                && relation.Target.Id.Contains("IOrderQueries", StringComparison.Ordinal));

        // Documented pre-fix baseline (audit-B3 / F4, GCPC-016): the two framework calls (Ok, NotFound)
        // hit InvokesPass's silent framework skip today — no candidate, no confirmed relation, no
        // unresolved record and no diagnostic. GCPC-016's counted exclusion ledger is a later phase (T23).
        Assert.DoesNotContain(
            candidates,
            link => link.Source.Id.Contains("OrderQueriesController", StringComparison.Ordinal)
                && link.Source.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && (link.ProposedTarget.Id.Contains("OkResult", StringComparison.Ordinal)
                    || link.ProposedTarget.Id.Contains("NotFoundResult", StringComparison.Ordinal)));
        var diagnosticsFragment = publication.ArtifactsInPublicationOrder
            .SingleOrDefault(artifact => artifact.CanonicalKey == "diagnostics.json");
        if (diagnosticsFragment is not null)
        {
            var diagnostics = CanonicalJson.Read<DiagnosticsEnvelope>(diagnosticsFragment.Payload.AsSpan());
            Assert.DoesNotContain(
                diagnostics.Records,
                record => record.IdentityOrKey is not null
                    && record.IdentityOrKey.Contains("OrderQueriesController", StringComparison.Ordinal));
        }
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey) =>
        CanonicalJson.Read<T>(
            Assert.Single(
                publication.ArtifactsInPublicationOrder,
                artifact => artifact.CanonicalKey == canonicalKey).Payload.AsSpan());

    private static async Task<CommittedPublication> AnalyzeCorpusAsync()
    {
        var solutionPath = CertificationCorpusPaths.SolutionPath;
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var store = new InMemoryTransactionalStore();
        var result = await new AnalysisEngine(store).AnalyzeAsync(
            AnalysisRequest.Create([solutionPath]),
            CancellationToken.None);

        var outcome = Assert.Single(result.Solutions);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
        Assert.True(store.TryGetPublication(Path.GetFullPath(solutionPath), out var publication));
        return publication;
    }
}
