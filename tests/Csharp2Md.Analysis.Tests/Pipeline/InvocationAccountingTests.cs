using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Analysis.Tests.Fixtures;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Pipeline;

/// <summary>
/// GCPC-012, GCPC-014, GCPC-015, GCPC-088, GCPC-089: the invocation- and contract-accounting ledgers
/// published by <see cref="ClassificationAndPromotionStage"/> from <c>InvokesPass</c>'s own disposition
/// ledger, and independently, from what <c>ContractPass</c>/<c>BoundaryPass</c> already publish.
/// </summary>
public sealed class InvocationAccountingTests
{
    [Fact]
    [Trait("Requirement", "GCPC-012")]
    [Trait("Requirement", "GCPC-015")]
    public void Build_OnePerDispositionOccurrenceEach_TotalsSumToRecognizedOccurrences()
    {
        var confirmedOccurrence = Occurrence(1);
        var candidateOccurrence = Occurrence(2);
        var unresolvedOccurrence = Occurrence(3);
        var excludedOccurrence = Occurrence(4);

        var ledger = new InvocationDispositionLedger();
        ledger.Add(InvocationDisposition.Create(confirmedOccurrence, InvocationDispositionKind.Confirmed));
        ledger.Add(InvocationDisposition.Create(candidateOccurrence, InvocationDispositionKind.Candidate));
        ledger.Add(InvocationDisposition.Create(unresolvedOccurrence, InvocationDispositionKind.Unresolved));
        ledger.Add(InvocationDisposition.Create(excludedOccurrence, InvocationExclusionCategory.ExternalFrameworkCallable));

        var recognized = new[] { confirmedOccurrence, candidateOccurrence, unresolvedOccurrence, excludedOccurrence };

        var report = InvocationAccounting.Build(recognized, ledger, openFrontierOccurrences: []);

        Assert.Equal(4, report.RecognizedOccurrences);
        Assert.Equal(
            report.RecognizedOccurrences,
            report.Confirmed + report.Candidate + report.Unresolved + report.Exclusions.Sum(e => e.Count));
        Assert.Empty(report.UnaccountedOccurrences);
    }

    [Fact]
    [Trait("Requirement", "GCPC-014")]
    public void Build_OccurrenceWithBothUnresolvedRecordAndOpenFrontier_IsCountedOnceInTheExclusiveTotal()
    {
        var occurrence = Occurrence(1);
        var ledger = new InvocationDispositionLedger();
        ledger.Add(InvocationDisposition.Create(occurrence, InvocationDispositionKind.Unresolved));

        var report = InvocationAccounting.Build([occurrence], ledger, openFrontierOccurrences: [occurrence]);

        Assert.Equal(1, report.RecognizedOccurrences);
        Assert.Equal(1, report.Unresolved);
        Assert.Equal(0, report.Confirmed);
        Assert.Equal(0, report.Candidate);
        Assert.Empty(report.Exclusions);
        // The frontier is an overlay, not a sixth exclusive bucket: the occurrence is still counted once,
        // not twice, in the total that must equal RecognizedOccurrences (GCPC-015).
        Assert.Equal(1, report.Confirmed + report.Candidate + report.Unresolved + report.Exclusions.Sum(e => e.Count));
        Assert.Equal(1, report.OpenFrontier);
    }

    [Fact]
    [Trait("Requirement", "GCPC-013")]
    public void Build_OccurrenceWithNoDisposition_IsNamedAsUnaccounted()
    {
        var dispositioned = Occurrence(1);
        var undispositioned = Occurrence(2);
        var ledger = new InvocationDispositionLedger();
        ledger.Add(InvocationDisposition.Create(dispositioned, InvocationDispositionKind.Confirmed));

        var report = InvocationAccounting.Build([dispositioned, undispositioned], ledger, openFrontierOccurrences: []);

        var unaccounted = Assert.Single(report.UnaccountedOccurrences);
        Assert.Contains(undispositioned.Owner.Id.Value, unaccounted, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-012")]
    public async Task ExecuteAsync_CertificationCorpus_InvocationAccountingTotalsSumToRecognizedOccurrences()
    {
        var snapshot = await RunThroughClassificationAsync(CertificationCorpusPaths.SolutionPath);
        var report = snapshot.InvocationAccounting;

        Assert.NotNull(report);
        var recognizedOccurrences = snapshot.Observations
            .Count(o => o.Identity.Kind is ObservationKind.Invocation or ObservationKind.ObjectCreation);

        Assert.True(recognizedOccurrences > 0);
        Assert.Equal(recognizedOccurrences, report.RecognizedOccurrences);
        Assert.Equal(
            report.RecognizedOccurrences,
            report.Confirmed + report.Candidate + report.Unresolved + report.Exclusions.Sum(e => e.Count));
        Assert.Empty(report.UnaccountedOccurrences);
    }

    [Fact]
    [Trait("Requirement", "GCPC-088")]
    [Trait("Requirement", "GCPC-089")]
    public async Task ExecuteAsync_CertificationCorpus_ContractAccountingTotalsSumToRecognizedTotal()
    {
        var snapshot = await RunThroughClassificationAsync(CertificationCorpusPaths.SolutionPath);
        var report = snapshot.ContractAccounting;

        Assert.NotNull(report);
        var independentRecognizedTotal =
            snapshot.Observations.Count(o => o.Identity.Kind is ObservationKind.MessageOperation)
            + snapshot.Facts.OfType<BoundaryOperation>().Count(operation => operation.Protocol is BoundaryProtocol.Messaging);

        Assert.True(independentRecognizedTotal > 0);
        Assert.Equal(independentRecognizedTotal, report.RecognizedTotal);
        Assert.Equal(
            report.RecognizedTotal,
            report.Contracted + report.Candidate + report.Unresolved + report.Exclusions.Sum(e => e.Count));
    }

    private static ObservationIdentity Occurrence(int ordinal) =>
        new(CallableReference(), ObservationKind.Invocation, NormalizedPayload.Create([]), ordinal);

    private static FactReference CallableReference() =>
        Symbol.Create(
                CanonicalSymbolSignature.Create("method", "global::Acme.Fixtures.Widget", "Do", 0, "global::System.Void"),
                FixturesProject,
                SymbolFacetSet.Create([SymbolFacet.Callable]))
            .Reference;

    private static SolutionId FixturesSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("fixtures"), "Fixtures.slnx");

    private static ProjectId FixturesProject =>
        ProjectId.Create(FixturesSolution, "Acme.Fixtures/Acme.Fixtures.csproj");

    private static async Task<FactualSnapshot> RunThroughClassificationAsync(string solutionPath)
    {
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var context = new PipelineContext(new SwallowingSession(), solutionPath);
        foreach (var stage in PipelineStages.CreateDefault().Take(4))
        {
            await stage.ExecuteAsync(context, CancellationToken.None);
        }

        return context.Accumulator.ToSnapshot();
    }
}
