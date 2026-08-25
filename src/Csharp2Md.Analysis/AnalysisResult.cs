namespace Csharp2Md.Analysis;

public sealed record AnalysisResult
{
    public ImmutableArray<SolutionOutcome> Solutions { get; }

    public bool HasUnpublishedSolution =>
        !Solutions.IsDefaultOrEmpty
        && Solutions.Any(static outcome => outcome.Status == PublicationStatus.Unpublished);

    public AnalysisResult(ImmutableArray<SolutionOutcome> solutions)
    {
        Solutions = solutions;
    }
}

public sealed record SolutionOutcome
{
    public string SolutionPath { get; }

    public string LogicalRelativePath { get; }

    public PublicationStatus Status { get; }

    public string? FailingStage { get; }

    public bool StructuralCorruption { get; }

    public bool HasUnknownsOrCandidatesOrFrontiers { get; }

    public ImmutableArray<StageReport> Stages { get; }

    public SolutionOutcome(
        string solutionPath,
        string logicalRelativePath,
        PublicationStatus status,
        string? failingStage,
        bool structuralCorruption,
        bool hasUnknownsOrCandidatesOrFrontiers,
        ImmutableArray<StageReport> stages)
    {
        SolutionPath = solutionPath;
        LogicalRelativePath = logicalRelativePath;
        Status = status;
        FailingStage = failingStage;
        StructuralCorruption = structuralCorruption;
        HasUnknownsOrCandidatesOrFrontiers = hasUnknownsOrCandidatesOrFrontiers;
        Stages = stages;
    }
}

public sealed record StageReport(string Name, int FactCount, int ObservationCount, int RelationCount);

public enum PublicationStatus
{
    Committed,
    Unpublished,
}
