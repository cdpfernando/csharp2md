using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Mapping;

/// <summary>
/// GCPC-004 (fix for the Verifier's F4): a layout-time degradation (<see cref="LayoutPlanner"/>'s
/// <c>record-exceeds-ceiling</c> reason for a solitary oversized <c>invokes</c>/<c>accesses-data</c>/
/// <c>uses-contract</c> relation record) reaches the published <c>coverage.json</c> as a real reason on
/// the metric it affects, rather than staying computed and unread as it did before this fix -- closing
/// the vacuous-satisfaction gap where GCPC-004 passed only because the published <c>reasons</c> array was
/// always empty.
/// </summary>
public sealed class CoverageDegradationRoutingTests
{
    private const string SolutionKey = @"C:\src\Acme.sln";

    [Fact]
    [Trait("Requirement", "GCPC-004")]
    public void Publish_OversizedInvokesRelation_RoutesADegradationReasonOntoLinkedCallCoverage()
    {
        var coordinate = SolutionCoordinate.For(SolutionKey);

        var outcome = PublicationPipeline.Publish(
            ManyInvokesSnapshot(count: 3),
            new ManifestContext(coordinate.Identity.Value, coordinate.SolutionFileName),
            coordinate,
            "s-test",
            projector: null,
            composer: null,
            EmptySourceDocumentReader.Instance,
            createView: null,
            readingBudgetTokens: 1,
            maxFileReadsPerScenario: 1);

        var coverage = ReadCoverage(outcome);

        var reasons = coverage.LinkedCallCoverage.DegradationReasons;
        Assert.NotEmpty(reasons);
        Assert.All(reasons, static reason => Assert.Equal("record-exceeds-ceiling", reason.Code));
        Assert.All(reasons, static reason => Assert.Equal(1, reason.AffectedCount));

        // Only the metric the degraded family actually feeds carries a reason -- the others stay clean.
        Assert.Empty(coverage.EntryPointCoverage.DegradationReasons);
        Assert.Empty(coverage.ContractCoverage.DegradationReasons);
        Assert.Empty(coverage.PersistenceCoverage.DegradationReasons);

        var certification = ReadCertification(outcome);
        var runReasons = certification.Reasons
            .Where(static reason => reason.StartsWith("record-exceeds-ceiling;", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(runReasons);
        Assert.All(
            runReasons,
            static reason => Assert.Contains("affected_count=1", reason, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "GCPC-004")]
    public void Publish_SameSnapshotUnderTheRealDefaultCeiling_PublishesAnEmptyReasonsArray()
    {
        var coordinate = SolutionCoordinate.For(SolutionKey);

        // Same fixture as the degraded case, but with no ceiling override: three tiny relation records
        // fit comfortably inside the real ~32 KiB default (T52), so nothing degrades.
        var outcome = PublicationPipeline.Publish(
            ManyInvokesSnapshot(count: 3),
            new ManifestContext(coordinate.Identity.Value, coordinate.SolutionFileName),
            coordinate,
            "s-test",
            projector: null,
            composer: null,
            EmptySourceDocumentReader.Instance);

        var coverage = ReadCoverage(outcome);

        Assert.Empty(coverage.LinkedCallCoverage.DegradationReasons);
        Assert.Empty(coverage.EntryPointCoverage.DegradationReasons);
        Assert.Empty(coverage.ContractCoverage.DegradationReasons);
        Assert.Empty(coverage.PersistenceCoverage.DegradationReasons);
        Assert.DoesNotContain(
            ReadCertification(outcome).Reasons,
            static reason => reason.StartsWith("record-exceeds-ceiling;", StringComparison.Ordinal));
    }

    private static CoverageEnvelope ReadCoverage(PublicationOutcome outcome)
    {
        var fragment = outcome.Fragments.Single(static f => f.CanonicalKey == "coverage.json");
        return CanonicalJson.Read<CoverageEnvelope>(fragment.Payload.AsSpan());
    }

    private static RunCertificationEnvelope ReadCertification(PublicationOutcome outcome)
    {
        var fragment = outcome.Fragments.Single(static f => f.CanonicalKey == "run-certification.json");
        return CanonicalJson.Read<RunCertificationEnvelope>(fragment.Payload.AsSpan());
    }

    private static FactualSnapshot ManyInvokesSnapshot(int count)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Orders/Acme.Orders.csproj");
        var caller = CallerSymbol(projectId);

        var facts = new List<IFact> { caller };
        var relations = new List<ConfirmedRelation>();
        for (var i = 0; i < count; i++)
        {
            var callee = CalleeSymbol(projectId, i);
            facts.Add(callee);
            relations.Add(Invokes(caller, callee.Reference, occurrenceOrdinal: i + 1));
        }

        // A hand-computed coverage report, the same shape ValidationAndCoverageStage would have produced
        // for these relations -- entry_point/contract/persistence have no recognizable population in this
        // minimal fixture, so they publish not_applicable; linked_call is fully evaluated and confirmed.
        var linkedCallCoverage = CoverageMetric.Evaluated(numerator: count, denominator: count, exclusions: 0, unknowns: 0);
        var notApplicable = CoverageMetric.NotApplicable("No recognizable population in this fixture.");
        var coverage = new CoverageReport(notApplicable, linkedCallCoverage, notApplicable, notApplicable);

        return new FactualSnapshot([.. facts], [], [.. relations], [], [], [], coverage: coverage);
    }

    private static Symbol CallerSymbol(ProjectId projectId) =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                "global::Acme.Orders.OrderService",
                "PlaceOrderAsync",
                0,
                "global::System.Threading.Tasks.Task"),
            projectId,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    private static Symbol CalleeSymbol(ProjectId projectId, int index) =>
        Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                $"global::Acme.Orders.Handler{index:D3}",
                "HandleAsync",
                0,
                "global::System.Threading.Tasks.Task"),
            projectId,
            SymbolFacetSet.Create([SymbolFacet.Callable]));

    private static ConfirmedRelation Invokes(Symbol source, FactReference target, int occurrenceOrdinal) =>
        ConfirmedRelation.Create(
            RelationKind.Invokes,
            source.Reference,
            target,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(
                    source.Reference,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    occurrenceOrdinal)]),
            ClassifierIdentity.Create("csharp2md.structural.invokes", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Semantic,
            sourceFact: source);
}
