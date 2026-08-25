namespace Csharp2Md.Analysis.Tests;

public sealed class AnalysisResultTests
{
    [Fact]
    [Trait("Requirement", "ENG-10")]
    public void HasUnpublishedSolution_IsFalse_WhenEveryOutcomeIsCommitted()
    {
        var result = new AnalysisResult([Committed("a.sln"), Committed("b.sln")]);

        Assert.False(result.HasUnpublishedSolution);
    }

    [Fact]
    [Trait("Requirement", "ENG-10")]
    public void HasUnpublishedSolution_IsTrue_WhenAnyOutcomeIsUnpublished()
    {
        var result = new AnalysisResult([Committed("a.sln"), Unpublished("b.sln")]);

        Assert.True(result.HasUnpublishedSolution);
    }

    [Fact]
    [Trait("Requirement", "ENG-10")]
    public void HasUnpublishedSolution_IsTrue_WhenEveryOutcomeIsUnpublished()
    {
        var result = new AnalysisResult([Unpublished("a.sln")]);

        Assert.True(result.HasUnpublishedSolution);
    }

    [Fact]
    [Trait("Requirement", "ENG-10")]
    public void HasUnpublishedSolution_IsFalse_WhenThereAreNoOutcomes()
    {
        var result = new AnalysisResult([]);

        Assert.False(result.HasUnpublishedSolution);
    }

    [Fact]
    [Trait("Requirement", "ENG-34")]
    public void SolutionOutcome_CarriesLogicalRelativePath()
    {
        var outcome = new SolutionOutcome(
            solutionPath: @"C:\src\Acme.sln",
            logicalRelativePath: "src/Acme.sln",
            status: PublicationStatus.Committed,
            failingStage: null,
            structuralCorruption: false,
            hasUnknownsOrCandidatesOrFrontiers: false,
            stages: []);

        Assert.Equal("src/Acme.sln", outcome.LogicalRelativePath);
        Assert.Equal(@"C:\src\Acme.sln", outcome.SolutionPath);
        Assert.Equal(PublicationStatus.Committed, outcome.Status);
    }

    [Fact]
    [Trait("Requirement", "ENG-10")]
    public void StageReport_CarriesNameAndThreeZeroableCounts()
    {
        var zeros = new StageReport("Inventory", 0, 0, 0);
        var counts = new StageReport("Persistence", 3, 2, 1);

        Assert.Equal("Inventory", zeros.Name);
        Assert.Equal(0, zeros.FactCount);
        Assert.Equal(0, zeros.ObservationCount);
        Assert.Equal(0, zeros.RelationCount);

        Assert.Equal("Persistence", counts.Name);
        Assert.Equal(3, counts.FactCount);
        Assert.Equal(2, counts.ObservationCount);
        Assert.Equal(1, counts.RelationCount);
    }

    [Fact]
    [Trait("Requirement", "ENG-10")]
    public void PublicationStatus_HasExactlyCommittedAndUnpublished()
    {
        Assert.Equal(["Committed", "Unpublished"], Enum.GetNames<PublicationStatus>());
        Assert.Equal(PublicationStatus.Committed, (PublicationStatus)0);
        Assert.Equal(PublicationStatus.Unpublished, (PublicationStatus)1);
    }

    private static SolutionOutcome Committed(string path) =>
        new(
            path,
            path,
            PublicationStatus.Committed,
            failingStage: null,
            structuralCorruption: false,
            hasUnknownsOrCandidatesOrFrontiers: false,
            stages: []);

    private static SolutionOutcome Unpublished(string path) =>
        new(
            path,
            path,
            PublicationStatus.Unpublished,
            failingStage: null,
            structuralCorruption: false,
            hasUnknownsOrCandidatesOrFrontiers: false,
            stages: []);
}
