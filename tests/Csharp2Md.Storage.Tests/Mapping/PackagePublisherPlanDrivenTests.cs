using Csharp2Md.Analysis.Storage;
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

/// <summary>GCPC-038/GCPC-040: publication is driven by the plan, writes every planned artifact exactly
/// once, writes nothing else, and keeps the manifest last.</summary>
public sealed class PackagePublisherPlanDrivenTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void ToPublicationOrder_GivenAPlan_WritesEveryPlannedArtifactExactlyOnceAndNothingElse()
    {
        var document = DomainMapper.ToWire(FilledSnapshot(), Context);
        var plan = LayoutPlanner.Plan(document, int.MaxValue);

        var fragments = PackagePublisher.ToPublicationOrder(document, plan);

        var payloadKeys = fragments
            .Where(fragment => fragment.Role == ArtifactRole.Payload)
            .Select(fragment => fragment.CanonicalKey)
            .ToArray();
        var plannedKeys = plan.Artifacts.Select(static artifact => artifact.ArtifactKey).ToArray();

        Assert.Equal(plannedKeys.Length, payloadKeys.Length);
        Assert.Equal(
            plannedKeys.OrderBy(static key => key, StringComparer.Ordinal),
            payloadKeys.OrderBy(static key => key, StringComparer.Ordinal));
        Assert.Equal(payloadKeys.Length, payloadKeys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ToPublicationOrder_GivenAPlan_ManifestIsAlwaysTheLastFragment()
    {
        var document = DomainMapper.ToWire(FilledSnapshot(), Context);
        var plan = LayoutPlanner.Plan(document, int.MaxValue);

        var fragments = PackagePublisher.ToPublicationOrder(document, plan);

        var last = fragments[^1];
        Assert.Equal(ArtifactRole.Manifest, last.Role);
        Assert.Equal(PackagePublisher.ManifestKey, last.CanonicalKey);
        Assert.DoesNotContain(
            fragments.Take(fragments.Length - 1),
            fragment => fragment.Role == ArtifactRole.Manifest);
    }

    [Fact]
    public void ToPublicationOrder_SingleArgOverload_AgreesWithThePlanOverloadUsingAnUnboundedCeiling()
    {
        var document = DomainMapper.ToWire(FilledSnapshot(), Context);

        var viaSingleArg = PackagePublisher.ToPublicationOrder(document);
        var viaPlan = PackagePublisher.ToPublicationOrder(document, LayoutPlanner.Plan(document, int.MaxValue));

        var singleArgKeys = viaSingleArg.Select(static fragment => fragment.CanonicalKey).OrderBy(static key => key, StringComparer.Ordinal);
        var viaPlanKeys = viaPlan.Select(static fragment => fragment.CanonicalKey).OrderBy(static key => key, StringComparer.Ordinal);
        Assert.Equal(viaPlanKeys, singleArgKeys);
    }

    private static FactualSnapshot FilledSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var otherProjectId = ProjectId.Create(solutionId, "src/Acme.Orders/Acme.Orders.csproj");
        var solution = Solution.Create(solutionId);
        var project = Project.Create(projectId);
        var other = Project.Create(otherProjectId);
        var component = Component.Create(solutionId, "Payments.Api", []);
        var binding = ConfigurationBinding.Create(
            project.Reference,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, "ConnectionStrings:Orders", "configurationKey"));
        var firstRelation = Contains(solution.Reference, project.Reference, occurrenceOrdinal: 1);
        var secondRelation = Contains(solution.Reference, other.Reference, occurrenceOrdinal: 2);
        return new FactualSnapshot(
            [solution, project, other, component, binding],
            [],
            [firstRelation, secondRelation],
            [],
            [],
            []);
    }

    private static ConfirmedRelation Contains(FactReference source, FactReference target, int occurrenceOrdinal) =>
        ConfirmedRelation.Create(
            RelationKind.Contains,
            source,
            target,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(
                    source,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    occurrenceOrdinal)]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
}
