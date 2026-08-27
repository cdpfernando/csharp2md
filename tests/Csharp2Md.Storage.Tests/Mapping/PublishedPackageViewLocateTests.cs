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

public sealed class PublishedPackageViewLocateTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    public void TryLocate_EveryFactInFilledDocument_CitedOrdinalYieldsThatFact()
    {
        var snapshot = FilledSnapshot();
        var view = ViewOf(snapshot);

        Assert.NotEmpty(snapshot.Facts);
        foreach (var fact in snapshot.Facts)
        {
            Assert.True(view.TryLocate(fact.Reference.Id.Value, out var citation));
            Assert.Equal(fact.Reference.Id.Value, FactIdAt(view, citation));
            Assert.True(citation.Ordinal >= 0);
        }
    }

    [Fact]
    public void TryLocate_UnknownFactId_ReturnsFalse()
    {
        var view = ViewOf(FilledSnapshot());

        Assert.False(view.TryLocate("missing-fact-id", out var citation));
        Assert.Equal(default, citation);
        Assert.False(view.TryLocate(string.Empty, out _));
    }

    [Fact]
    public void TryLocateRelation_PublishedIndex_ReturnsKeyAndZeroBasedOrdinal()
    {
        var view = ViewOf(FilledSnapshot());
        var records = view.Document.ConfirmedRelations["contains"];
        Assert.Equal(2, records.Length);

        for (var index = 0; index < records.Length; index++)
        {
            Assert.True(view.TryLocateRelation("contains", index, out var citation));
            Assert.Equal("relations/confirmed/contains.json", citation.ArtifactKey);
            Assert.Equal(index, citation.Ordinal);
            Assert.Equal(records[citation.Ordinal].Source.Id, records[index].Source.Id);
            Assert.Equal(records[citation.Ordinal].Target.Id, records[index].Target.Id);
        }
    }

    [Fact]
    public void TryLocateRelation_UnknownKind_ReturnsFalse()
    {
        var view = ViewOf(FilledSnapshot());

        Assert.False(view.TryLocateRelation("invokes", 0, out var citation));
        Assert.Equal(default, citation);
    }

    [Fact]
    public void TryLocateRelation_OutOfRangeIndex_ReturnsFalse()
    {
        var view = ViewOf(FilledSnapshot());

        Assert.False(view.TryLocateRelation("contains", -1, out _));
        Assert.False(view.TryLocateRelation("contains", 2, out _));
        Assert.False(view.TryLocateRelation(string.Empty, 0, out _));
    }

    [Fact]
    public void TryLocate_FirstStructuralFact_HasOrdinalZero()
    {
        var snapshot = FilledSnapshot();
        var view = ViewOf(snapshot);
        var firstId = view.Document.Solutions[0].Identity.Id;

        Assert.True(view.TryLocate(firstId, out var citation));
        Assert.Equal("facts/structural.json", citation.ArtifactKey);
        Assert.Equal(0, citation.Ordinal);
        Assert.Equal(firstId, FactIdAt(view, citation));
    }

    private static PublishedPackageView ViewOf(FactualSnapshot snapshot) =>
        PublishedPackageView.From(DomainMapper.ToWire(snapshot, Context));

    private static string FactIdAt(PublishedPackageView view, ArtifactCitation citation)
    {
        var ids = citation.ArtifactKey switch
        {
            "facts/structural.json" => Ids(
                view.Document.Solutions.Select(static dto => dto.Identity.Id),
                view.Document.Projects.Select(static dto => dto.Identity.Id),
                view.Document.Documents.Select(static dto => dto.Identity.Id),
                view.Document.Symbols.Select(static dto => dto.Identity.Id)),
            "facts/architecture.json" => Ids(
                view.Document.Components.Select(static dto => dto.Identity.Id),
                view.Document.DeploymentUnits.Select(static dto => dto.Identity.Id),
                view.Document.EntryPoints.Select(static dto => dto.Identity.Id),
                view.Document.BoundaryOperations.Select(static dto => dto.Identity.Id),
                view.Document.ExternalSystems.Select(static dto => dto.Identity.Id)),
            "facts/configuration.json" => Ids(
                view.Document.ConfigurationBindings.Select(static dto => dto.Identity.Id)),
            _ => throw new InvalidOperationException($"Unexpected artifact '{citation.ArtifactKey}'."),
        };
        return ids[citation.Ordinal];
    }

    private static ImmutableArray<string> Ids(params IEnumerable<string>[] sequences) =>
        [.. sequences.SelectMany(static sequence => sequence)];

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
