namespace Csharp2Md.Analysis;

public sealed record AnalysisResult
{
    public ImmutableArray<SolutionOutcome> Solutions { get; }

    public string? BatchPublicationGate { get; }

    public string? BatchPublicationDetail { get; }

    public bool HasUnpublishedSolution =>
        !Solutions.IsDefaultOrEmpty
        && Solutions.Any(static outcome => outcome.Status == PublicationStatus.Unpublished);

    public bool HasBatchPublicationFailure => BatchPublicationGate is { Length: > 0 };

    public AnalysisResult(
        ImmutableArray<SolutionOutcome> solutions,
        string? batchPublicationGate = null,
        string? batchPublicationDetail = null)
    {
        Solutions = solutions;
        BatchPublicationGate = batchPublicationGate;
        BatchPublicationDetail = batchPublicationDetail;
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

    public string? Detail { get; }

    public SolutionOutcome(
        string solutionPath,
        string logicalRelativePath,
        PublicationStatus status,
        string? failingStage,
        bool structuralCorruption,
        bool hasUnknownsOrCandidatesOrFrontiers,
        ImmutableArray<StageReport> stages,
        string? detail = null)
    {
        SolutionPath = solutionPath;
        LogicalRelativePath = logicalRelativePath;
        Status = status;
        FailingStage = failingStage;
        StructuralCorruption = structuralCorruption;
        HasUnknownsOrCandidatesOrFrontiers = hasUnknownsOrCandidatesOrFrontiers;
        Stages = stages;
        Detail = detail;
    }
}

public sealed record StageReport(string Name, int FactCount, int ObservationCount, int RelationCount);

public enum PublicationStatus
{
    Committed,
    Unpublished,
}
