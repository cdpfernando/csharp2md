using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

namespace Csharp2Md.Analysis.Inventory;

internal sealed class InventoryStage : IPipelineStage
{
    public string Name => "Inventory";

    public ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        var solutionPath = Path.GetFullPath(context.SolutionPath);
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(solutionPath)
            ?? throw new InvalidOperationException($"'{solutionPath}' has no containing directory.");

        var existing = new List<string>();
        var missing = new List<string>();
        foreach (var listedPath in listed)
        {
            var absolute = Path.GetFullPath(Path.Combine(solutionDirectory, listedPath));
            if (File.Exists(absolute))
            {
                existing.Add(absolute);
            }
            else
            {
                missing.Add(absolute);
            }
        }

        var root = AuthorizedRoot.Compute(solutionPath, existing);
        foreach (var absent in missing)
        {
            var relative = Path.GetRelativePath(root, absent).Replace('\\', '/');
            context.Accumulator.AddDiagnostic(new DiagnosticRecord(
                "missing-project",
                $"The listed project path '{relative}' does not exist.",
                relative));
        }

        return ValueTask.FromResult(new StageResult(
            0,
            0,
            0,
            StructuralCorruption: false,
            HasUnknownsOrCandidatesOrFrontiers: missing.Count > 0));
    }
}
