using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Storage;

public readonly record struct SolutionCoordinate
{
    public SolutionId Identity { get; }

    public string SolutionFileName { get; }

    public static SolutionCoordinate For(string solutionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        var fileName = Path.GetFileName(solutionPath);
        return new SolutionCoordinate(
            SolutionId.Create(WorkspaceIdentity.Create("default"), fileName),
            fileName);
    }

    private SolutionCoordinate(SolutionId identity, string solutionFileName)
    {
        Identity = identity;
        SolutionFileName = solutionFileName;
    }
}
