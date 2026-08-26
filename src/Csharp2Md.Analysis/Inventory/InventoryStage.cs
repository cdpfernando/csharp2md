using System.Xml.Linq;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

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
        if (!TryGuard(context, root, solutionPath, out var abort))
        {
            return ValueTask.FromResult(abort);
        }

        var factSet = InventoryFacts.Create(solutionPath, listed, root);
        context.Accumulator.AddFact(factSet.Solution);
        var factCount = 1;
        var csharpDocuments = ImmutableArray.CreateBuilder<Document>();
        var targetFrameworks = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var projectPath in existing)
        {
            if (!TryGuard(context, root, projectPath, out abort))
            {
                return ValueTask.FromResult(abort);
            }

            var relativeProject = Path.GetRelativePath(root, projectPath).Replace('\\', '/');
            var projectId = ProjectId.Create(factSet.Solution.Id, relativeProject);
            var project = factSet.Projects.First(candidate => candidate.Id.Equals(projectId));
            context.Accumulator.AddFact(project);
            factCount++;

            foreach (var tfm in ReadDeclaredTargetFrameworks(projectPath))
            {
                targetFrameworks.Add(tfm);
            }

            var inventoried = DocumentInventory.Collect(root, project, projectPath, existing);
            foreach (var document in inventoried.Documents)
            {
                var absolute = Path.GetFullPath(
                    Path.Combine(root, document.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!TryGuard(context, root, absolute, out abort))
                {
                    return ValueTask.FromResult(abort);
                }

                context.Accumulator.AddFact(document);
                factCount++;
            }

            foreach (var diagnostic in inventoried.Diagnostics)
            {
                context.Accumulator.AddDiagnostic(diagnostic);
            }

            csharpDocuments.AddRange(inventoried.CSharpDocuments);
        }

        foreach (var absent in missing)
        {
            var relative = Path.GetRelativePath(root, absent).Replace('\\', '/');
            context.Accumulator.AddDiagnostic(new DiagnosticRecord(
                "missing-project",
                $"The listed project path '{relative}' does not exist.",
                relative));
        }

        context.DeclaredTargetFrameworks = [.. targetFrameworks];
        context.CSharpDocuments = csharpDocuments.ToImmutable();

        return ValueTask.FromResult(new StageResult(
            factCount,
            0,
            0,
            StructuralCorruption: false,
            HasUnknownsOrCandidatesOrFrontiers: missing.Count > 0));
    }

    private static ImmutableArray<string> ReadDeclaredTargetFrameworks(string projectFilePath)
    {
        var document = XDocument.Load(projectFilePath);
        var frameworks = ImmutableArray.CreateBuilder<string>();
        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName == "TargetFramework"
                && !string.IsNullOrWhiteSpace(element.Value))
            {
                frameworks.Add(element.Value.Trim());
            }
            else if (element.Name.LocalName == "TargetFrameworks"
                && !string.IsNullOrWhiteSpace(element.Value))
            {
                frameworks.AddRange(
                    element.Value.Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return frameworks.ToImmutable();
    }

    private static bool TryGuard(PipelineContext context, string root, string path, out StageResult abort)
    {
        try
        {
            PathGuard.RejectEscapes(root, path);
            abort = default;
            return true;
        }
        catch (InvalidOperationException)
        {
            context.Detail = path;
            abort = new StageResult(
                0,
                0,
                0,
                StructuralCorruption: false,
                HasUnknownsOrCandidatesOrFrontiers: false,
                AbortPublication: true);
            return false;
        }
    }
}
