using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Storage;

public sealed record BatchSolutionRecord(
    SolutionId Identity,
    string SolutionFileName,
    PublicationStatus Status,
    string? FailingStage);
