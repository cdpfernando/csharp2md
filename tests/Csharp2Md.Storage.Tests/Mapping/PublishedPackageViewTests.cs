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

public sealed class PublishedPackageViewTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void From_EmptySnapshot_OmitsFamilyShards()
    {
        var view = ViewOf(FactualSnapshot.Empty);

        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey.StartsWith("facts/", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey.StartsWith("relations/", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey.StartsWith("quarantine/", StringComparison.Ordinal));
    }

    [Fact]
    public void From_EmptySnapshot_IncludesAlwaysPublishedEnvelopeSlots()
    {
        var view = ViewOf(FactualSnapshot.Empty);
        var keys = view.Slots.Select(slot => slot.CanonicalKey).ToArray();

        Assert.Equal(
            [
                "contracts/taxonomy-registry.json",
                "coverage.json",
                "diagnostics.json",
                "measurements.json",
                "run-certification.json",
            ],
            keys);
        Assert.All(view.Slots, slot => Assert.Equal(ArtifactRole.Payload, slot.Role));
    }

    [Fact]
    public void From_FilledDocument_ExposesStructuralSlotWithElementCount()
    {
        var view = ViewOf(StructuralSnapshot());
        var slot = Assert.Single(view.Slots, candidate => candidate.CanonicalKey == "facts/structural.json");

        Assert.Equal(ArtifactRole.Payload, slot.Role);
        Assert.Equal(2, slot.Count);
    }

    [Fact]
    public void From_FilledDocument_ExposesObservationAndConfirmedRelationSlots()
    {
        var view = ViewOf(ObservationAndContainsSnapshot());

        var observation = Assert.Single(view.Slots, slot => slot.CanonicalKey == "observations/invocation.json");
        Assert.Equal(1, observation.Count);
        var relation = Assert.Single(view.Slots, slot => slot.CanonicalKey == "relations/confirmed/contains.json");
        Assert.Equal(1, relation.Count);
    }

    [Fact]
    public void From_DocumentWithEmptyFamilies_OmitsEmptyShards()
    {
        var view = ViewOf(StructuralSnapshot());

        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey == "facts/architecture.json");
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey == "facts/contract.json");
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey == "facts/persistence.json");
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey == "facts/configuration.json");
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey.StartsWith("observations/", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Slots, slot => slot.CanonicalKey.StartsWith("relations/", StringComparison.Ordinal));
    }

    [Fact]
    public void From_SlotsUseFragmentFormKeysInPublicationOrder()
    {
        var view = ViewOf(StructuralSnapshot().Merge(ObservationAndContainsSnapshot()));
        var keys = view.Slots.Select(slot => slot.CanonicalKey).ToArray();

        Assert.All(keys, key => Assert.EndsWith(".json", key, StringComparison.Ordinal));
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        Assert.Contains("facts/structural.json", keys);
        Assert.Contains("observations/invocation.json", keys);
        Assert.Contains("relations/confirmed/contains.json", keys);
        Assert.DoesNotContain("facts/structural", keys);
    }

    private static PublishedPackageView ViewOf(FactualSnapshot snapshot) =>
        PublishedPackageView.From(DomainMapper.ToWire(snapshot, Context));

    private static FactualSnapshot StructuralSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        return new FactualSnapshot(
            [Solution.Create(solutionId), Project.Create(projectId)],
            [],
            [],
            [],
            [],
            []);
    }

    private static FactualSnapshot ObservationAndContainsSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var owner = Solution.Create(solutionId).Reference;
        var observation = Observation.Create(
            owner,
            ObservationKind.Invocation,
            NormalizedPayload.Create([]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "src/Acme.Payments/Invoice.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("BIND001", "Bound successfully."),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
        var relation = ConfirmedRelation.Create(
            RelationKind.Contains,
            owner,
            Project.Create(projectId).Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([observation.Identity]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
        return new FactualSnapshot([], [observation], [relation], [], [], []);
    }
}
