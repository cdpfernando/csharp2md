using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class ObservationRelationRoundTripTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");

    public static TheoryData<ObservationKind> ObservationKinds()
    {
        var data = new TheoryData<ObservationKind>();
        foreach (var descriptor in TaxonomyTables.Default.ObservationKinds)
        {
            data.Add(descriptor.Kind);
        }

        return data;
    }

    [Theory]
    [Trait("Requirement", "STOR-02")]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-13")]
    [MemberData(nameof(ObservationKinds))]
    public void RoundTrip_ObservationKind_UsesWireNameAndEqualsOriginal(ObservationKind kind)
    {
        var wireName = TaxonomyTables.Default.ObservationKinds.Single(descriptor => descriptor.Kind == kind).WireName;
        var observation = CreateObservation(kind);
        var snapshot = new FactualSnapshot([], [observation], [], [], [], []);

        var document = DomainMapper.ToWire(snapshot, Context);
        var dto = Assert.Single(document.Observations[wireName]);
        Assert.Equal(wireName, dto.Identity.Kind);
        Assert.NotEqual(kind.ToString(), wireName);

        var restored = DomainMapper.FromWire(document);
        AssertObservationEqual(observation, Assert.Single(restored.Observations.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "STOR-03")]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-13")]
    public void RoundTrip_ConfirmedRelation_UsesWireNameAndEqualsOriginal()
    {
        var relation = CreateContainsRelation();
        var snapshot = new FactualSnapshot([], [], [relation], [], [], []);

        var document = DomainMapper.ToWire(snapshot, Context);
        var dto = Assert.Single(document.ConfirmedRelations["contains"]);
        Assert.Equal("contains", dto.Kind);
        Assert.Equal("Syntactic", dto.EvidenceMethod);
        Assert.Equal("csharp2md.structural.contains", dto.Classifier.Id);

        var restored = DomainMapper.FromWire(document);
        Assert.Equal(relation, Assert.Single(restored.ConfirmedRelations.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "STOR-03")]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-13")]
    public void RoundTrip_CandidateLink_EqualsOriginal()
    {
        var link = CandidateLink.Create(
            RelationKind.Contains,
            Solution.Create(AcmeSolution).Reference,
            Project.Create(AcmeProject).Reference,
            ValidEvidence());

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([], [], [], [link], [], []), Context));

        Assert.Equal(link, Assert.Single(restored.Candidates.ToArray()));
        Assert.Equal("contains", DomainMapper.ToWire(new FactualSnapshot([], [], [], [link], [], []), Context)
            .Candidates[0].Kind);
    }

    [Fact]
    [Trait("Requirement", "STOR-03")]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-13")]
    public void RoundTrip_UnresolvedRecord_EqualsOriginal()
    {
        var record = UnresolvedRecord.Create(
            RelationKind.Contains,
            Solution.Create(AcmeSolution).Reference,
            UnresolvedCause.AmbiguousTarget,
            ValidEvidence());

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([], [], [], [], [record], []), Context));

        Assert.Equal(record, Assert.Single(restored.Unresolved.ToArray()));
    }

    [Fact]
    [Trait("Requirement", "STOR-03")]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-13")]
    public void RoundTrip_OpenFrontier_EqualsOriginal()
    {
        var frontier = OpenFrontier.Create(ValidOccurrence(), FrontierCause.FurtherContinuationObserved);

        var restored = DomainMapper.FromWire(
            DomainMapper.ToWire(new FactualSnapshot([], [], [], [], [], [frontier]), Context));

        Assert.Equal(frontier, Assert.Single(restored.Frontiers.ToArray()));
    }

    private static Observation CreateObservation(ObservationKind kind) =>
        Observation.Create(
            Solution.Create(AcmeSolution).Reference,
            kind,
            NormalizedPayload.Create([]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));

    private static ConfirmedRelation CreateContainsRelation() =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            Solution.Create(AcmeSolution).Reference,
            Project.Create(AcmeProject).Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            ValidEvidence(),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);

    private static EvidenceChain ValidEvidence() =>
        EvidenceChain.Create([ValidOccurrence()]);

    private static ObservationIdentity ValidOccurrence() =>
        new(Solution.Create(AcmeSolution).Reference, ObservationKind.Invocation, NormalizedPayload.Create([]), 1);

    private static void AssertObservationEqual(Observation expected, Observation actual)
    {
        Assert.Equal(expected.Identity, actual.Identity);
        Assert.Equal(expected.Locator, actual.Locator);
        Assert.Equal(expected.ExtractionMethod, actual.ExtractionMethod);
        Assert.Equal(expected.Diagnostic, actual.Diagnostic);
        Assert.Equal(expected.DocumentHash, actual.DocumentHash);
        Assert.Equal(expected.ExtractorVersion, actual.ExtractorVersion);
    }
}
