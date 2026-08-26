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

public sealed class ConfirmedRelationOrderingTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    [Trait("Requirement", "STOR-03")]
    public void ToWire_SameKindAndEndpointsDifferentEvidence_OrdersIdenticallyAcrossInsertionOrder()
    {
        var alpha = CreateContainsRelation(occurrenceOrdinal: 1);
        var beta = CreateContainsRelation(occurrenceOrdinal: 2);
        Assert.NotEqual(ContentHash(alpha), ContentHash(beta));

        var forward = MappedContains(alpha, beta);
        var reverse = MappedContains(beta, alpha);

        Assert.Equal(2, forward.Length);
        Assert.Equal(forward[0].ContentSha256, reverse[0].ContentSha256);
        Assert.Equal(forward[1].ContentSha256, reverse[1].ContentSha256);
        Assert.NotEqual(forward[0].ContentSha256, forward[1].ContentSha256);
        Assert.True(
            string.Compare(forward[0].ContentSha256, forward[1].ContentSha256, StringComparison.Ordinal) < 0);
    }

    private static ImmutableArray<ConfirmedRelationDto> MappedContains(
        ConfirmedRelation first,
        ConfirmedRelation second)
    {
        var document = DomainMapper.ToWire(
            new FactualSnapshot([], [], [first, second], [], [], []),
            Context);
        return document.ConfirmedRelations["contains"];
    }

    private static string ContentHash(ConfirmedRelation relation) =>
        WireRelationMapping.ToDto(relation).ContentSha256;

    private static ConfirmedRelation CreateContainsRelation(int occurrenceOrdinal)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Payments/Acme.Payments.csproj");
        var owner = Solution.Create(solutionId).Reference;
        return ConfirmedRelation.Create(
            RelationKind.Contains,
            owner,
            Project.Create(projectId).Reference,
            FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []),
            EvidenceChain.Create([
                new ObservationIdentity(
                    owner,
                    ObservationKind.Invocation,
                    NormalizedPayload.Create([]),
                    occurrenceOrdinal)]),
            ClassifierIdentity.Create("csharp2md.structural.contains", 1),
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
    }
}
