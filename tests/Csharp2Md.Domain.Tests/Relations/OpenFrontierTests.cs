using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class OpenFrontierTests
{
    private static FactReference SolutionReference(string name) =>
        new(new FactId("solution", $"id1:solution;name={name}"), "Solution");

    private static FactReference ProjectReference(string name) =>
        new(new FactId("project", $"id1:project;name={name}"), "Project");

    private static ObservationIdentity Occurrence() =>
        new(SolutionReference("acme"), ObservationKind.Invocation, NormalizedPayload.Create([]), 1);

    private static ConfirmedRelation ConfirmedAtSameOccurrence()
    {
        var facets = FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);
        var evidence = EvidenceChain.Create([Occurrence()]);
        var classifier = ClassifierIdentity.Create("csharp2md.structural.contains", 1);

        return ConfirmedRelation.Create(
            RelationKind.Contains,
            SolutionReference("acme"),
            ProjectReference("payments"),
            facets,
            evidence,
            classifier,
            [AnalysisVariantId.Create("net10.0", "Release", [], "ci")],
            EvidenceMethod.Syntactic);
    }

    [Fact]
    [Trait("Requirement", "TAX-65")]
    public void Create_IsKeyedOnTheOccurrence_NotOnTheRelation()
    {
        var occurrence = Occurrence();

        var frontier = OpenFrontier.Create(occurrence, FrontierCause.FurtherContinuationObserved);

        Assert.Equal(occurrence, frontier.Occurrence);
        Assert.DoesNotContain(
            typeof(OpenFrontier).GetProperties(),
            property => property.PropertyType == typeof(ConfirmedRelation) || property.PropertyType == typeof(RelationKind));
    }

    [Fact]
    [Trait("Requirement", "TAX-65")]
    public void Create_AtAnOccurrenceThatAlreadyCarriesAConfirmedRelation_LeavesThatRelationByteIdentical()
    {
        var before = ConfirmedAtSameOccurrence();

        OpenFrontier.Create(Occurrence(), FrontierCause.FurtherContinuationObserved);
        var after = ConfirmedAtSameOccurrence();

        Assert.Equal(before, after);
    }

    [Fact]
    [Trait("Requirement", "TAX-65")]
    public void Frontier_ReadsOpen_AndHasNoSetter()
    {
        var frontier = OpenFrontier.Create(Occurrence(), FrontierCause.FurtherContinuationObserved);

        Assert.Equal(Frontier.Open, frontier.Frontier);
        Assert.Null(typeof(OpenFrontier).GetProperty(nameof(OpenFrontier.Frontier))!.GetSetMethod(nonPublic: true));
    }

    [Fact]
    [Trait("Requirement", "TAX-65")]
    public void Create_NullCause_IsRejectedNamingTheParameter()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => OpenFrontier.Create(Occurrence(), null));

        Assert.Equal("cause", exception.ParamName);
    }
}
