using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Passes;
using Csharp2Md.Analysis.Extraction;
using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Analysis.Tests.Fixtures;

/// <summary>
/// GCPC-018: reproduces the audit's dropped-invocation regression
/// (OrdersController.GetOrderAsync -> IOrderQueries.GetOrderAsync -> OrderQueries.GetOrderAsync) in
/// the versioned certification corpus. T23 closes GCPC-011/GCPC-016: the two framework calls
/// (<c>Ok</c>, <c>NotFound</c>) that <see cref="InvokesPass"/> used to skip in total silence are now
/// counted exclusions, and every recognized invocation occurrence in the corpus carries exactly one
/// disposition.
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
    public async Task AnalyzeAsync_CertificationCorpus_InterfaceCallIsExactlyOneCandidateNeverAConfirmedInvokesToTheInterface()
    {
        var publication = await AnalyzeCorpusAsync();

        var candidates = ReadShard<ImmutableArray<CandidateLinkDto>>(publication, "relations/candidates.json");
        var confirmedInvokes = ReadShard<ImmutableArray<ConfirmedRelationDto>>(
            publication,
            "relations/confirmed/invokes.json");

        // GCPC-018: an interface dispatch with a single concrete implementor resolves to exactly one
        // CandidateLink from the action to that concrete implementation, and never a confirmed
        // `invokes` to the interface member.
        var toConcreteImplementation = candidates
            .Where(link => link.Kind == "invokes"
                && link.Source.Id.Contains("OrderQueriesController", StringComparison.Ordinal)
                && link.Source.Id.Contains("GetOrderStatus", StringComparison.Ordinal)
                && link.ProposedTarget.Id.Contains("Certification.Queries", StringComparison.Ordinal))
            .ToArray();
        var candidate = Assert.Single(toConcreteImplementation);
        Assert.Contains("GetOrderStatus", candidate.ProposedTarget.Id, StringComparison.Ordinal);
        Assert.DoesNotContain(
            confirmedInvokes,
            relation => relation.Kind == "invokes"
                && relation.Source.Id.Contains("OrderQueriesController", StringComparison.Ordinal)
                && relation.Target.Id.Contains("IOrderQueries", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-016")]
    public async Task AnalyzeAsync_CertificationCorpus_FrameworkCallsAreCountedExclusionsCarryingTheDeclaredCategory()
    {
        var (ledger, _) = await RunInvokesPassOverCorpusAsync();

        var getOrderStatusDispositions = ledger.Dispositions
            .Where(disposition => disposition.Occurrence.Owner.Id.Value.Contains("GetOrderStatus", StringComparison.Ordinal)
                && disposition.Occurrence.Owner.Id.Value.Contains("OrderQueriesController", StringComparison.Ordinal))
            .ToArray();

        // The action makes three calls: the interface dispatch (a candidate, asserted separately) and
        // the two framework calls (Ok, NotFound). Both framework calls are now counted exclusions
        // carrying the declared category (GCPC-016), not the total silence the audit found.
        var excluded = getOrderStatusDispositions
            .Where(disposition => disposition.Kind is InvocationDispositionKind.Excluded)
            .ToArray();
        Assert.Equal(2, excluded.Length);
        Assert.All(
            excluded,
            disposition => Assert.Equal(InvocationExclusionCategory.ExternalFrameworkCallable, disposition.ExclusionCategory));
        Assert.Contains(
            getOrderStatusDispositions,
            disposition => disposition.Kind is InvocationDispositionKind.Candidate);

        // No per-occurrence diagnostic is emitted for an excluded occurrence.
        var publication = await AnalyzeCorpusAsync();
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

    [Fact]
    [Trait("Requirement", "GCPC-011")]
    public async Task AnalyzeAsync_CertificationCorpus_EveryRecognizedInvocationOccurrenceCarriesExactlyOneDisposition()
    {
        var (ledger, context) = await RunInvokesPassOverCorpusAsync();

        // Denominator re-derived independently of the ledger: every Invocation and ObjectCreation
        // observation in the corpus is a recognized invocation occurrence (GCPC-011).
        var recognizedOccurrences = context.ObservationsByKind(ObservationKind.Invocation).Count()
            + context.ObservationsByKind(ObservationKind.ObjectCreation).Count();

        Assert.NotEmpty(ledger.Dispositions);
        Assert.Empty(ledger.Duplicates);
        Assert.Equal(recognizedOccurrences, ledger.Dispositions.Count);
    }

    private static async Task<(InvocationDispositionLedger Ledger, ClassifierContext Context)> RunInvokesPassOverCorpusAsync()
    {
        var solutionPath = CertificationCorpusPaths.SolutionPath;
        var pipelineContext = new PipelineContext(new SwallowingSession(), solutionPath);
        await new InventoryStage().ExecuteAsync(pipelineContext, CancellationToken.None);
        await new SemanticAnalysisStage().ExecuteAsync(pipelineContext, CancellationToken.None);
        await new ObservationExtractionStage().ExecuteAsync(pipelineContext, CancellationToken.None);
        var context = new ClassifierContext(pipelineContext);
        new InvokesPass().Execute(context, CancellationToken.None, out var ledger);
        return (ledger, context);
    }

    private static T ReadShard<T>(CommittedPublication publication, string canonicalKey) =>
        ShardedFactsReader.Read<T>(publication.ArtifactsInPublicationOrder, canonicalKey);

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
