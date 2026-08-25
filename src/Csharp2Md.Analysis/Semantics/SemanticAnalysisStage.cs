using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Identity;
using Microsoft.CodeAnalysis;
using DomainProject = Csharp2Md.Domain.Facts.Project;

namespace Csharp2Md.Analysis.Semantics;

internal sealed class SemanticAnalysisStage : IPipelineStage
{
    private readonly IMsBuildWorkspaceFactory _factory;

    public SemanticAnalysisStage()
        : this(new MsBuildWorkspaceFactory())
    {
    }

    internal SemanticAnalysisStage(IMsBuildWorkspaceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public string Name => "Semantic Analysis";

    public async ValueTask<StageResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        const string configuration = AnalysisVariantFactory.DefaultConfiguration;
        var compilations = ImmutableArray.CreateBuilder<Compilation>();
        var recordedSdkProjects = new HashSet<string>(StringComparer.Ordinal);
        var recordedCompileErrors = new HashSet<string>(StringComparer.Ordinal);
        var recordedVariants = new HashSet<string>(StringComparer.Ordinal);
        var variants = ImmutableArray.CreateBuilder<AnalysisVariantId>();
        var hasUnknowns = false;
        try
        {
            foreach (var targetFramework in context.DeclaredTargetFrameworks)
            {
                await using var lease = await _factory
                    .Open(context.SolutionPath, configuration, targetFramework, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var diagnostic in lease.Diagnostics)
                {
                    if (!IsUnresolvableSdk(diagnostic.Message))
                    {
                        continue;
                    }

                    var identity = RelativeProjectIdentity(diagnostic.Message, context);
                    if (!recordedSdkProjects.Add(identity))
                    {
                        continue;
                    }

                    hasUnknowns = true;
                    context.Accumulator.AddDiagnostic(new DiagnosticRecord(
                        "unresolvable-sdk",
                        $"The project SDK could not be resolved for '{identity}'.",
                        identity));
                }

                foreach (var project in lease.Solution.Projects)
                {
                    if (project.Language != LanguageNames.CSharp)
                    {
                        continue;
                    }

                    var compilation = await CompilationSanitizer.Strip(project)
                        .GetCompilationAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (compilation is null)
                    {
                        continue;
                    }

                    compilations.Add(compilation);
                    var variant = AnalysisVariantFactory.Create(project, targetFramework, configuration);
                    if (recordedVariants.Add(variant.Value))
                    {
                        variants.Add(variant);
                    }

                    if (!compilation.GetDiagnostics(cancellationToken)
                        .Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                    {
                        continue;
                    }

                    var identity = string.IsNullOrEmpty(project.FilePath)
                        ? project.Name.Replace('\\', '/')
                        : ToRelativeIdentity(project.FilePath, context.SolutionPath);
                    if (!recordedCompileErrors.Add(identity))
                    {
                        continue;
                    }

                    hasUnknowns = true;
                    context.Accumulator.AddDiagnostic(new DiagnosticRecord(
                        "compilation-error",
                        $"The project '{identity}' produced compilation diagnostics of error severity.",
                        identity));
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Detail = exception.Message;
            context.Accumulator.AddDiagnostic(new DiagnosticRecord(
                "msbuild-open-failed",
                exception.Message,
                Path.GetFileName(context.SolutionPath)));
            return new StageResult(
                0,
                0,
                0,
                StructuralCorruption: false,
                HasUnknownsOrCandidatesOrFrontiers: false,
                AbortPublication: true);
        }

        context.Compilations = compilations.ToImmutable();
        context.AnalysisVariants = variants.ToImmutable();
        return new StageResult(
            0,
            0,
            0,
            StructuralCorruption: false,
            HasUnknownsOrCandidatesOrFrontiers: hasUnknowns);
    }

    private static bool IsUnresolvableSdk(string message) =>
        message.Contains("SDK", StringComparison.OrdinalIgnoreCase);

    private static string RelativeProjectIdentity(string message, PipelineContext context)
    {
        var extracted = ExtractCsprojPath(message);
        if (extracted is not null)
        {
            return ToRelativeIdentity(extracted, context.SolutionPath);
        }

        foreach (var project in context.Accumulator.ToSnapshot().Facts.OfType<DomainProject>())
        {
            var relative = PathComponent(project.Id.Value);
            if (relative is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(relative);
            if (!string.IsNullOrEmpty(fileName)
                && message.Contains(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return relative;
            }
        }

        return "unresolvable-project";
    }

    private static string? ExtractCsprojPath(string message)
    {
        foreach (var part in message.Split('\'', '"'))
        {
            if (part.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return part;
            }
        }

        return null;
    }

    private static string ToRelativeIdentity(string projectPath, string solutionPath)
    {
        if (!Path.IsPathRooted(projectPath))
        {
            return projectPath.Replace('\\', '/');
        }

        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))
            ?? throw new InvalidOperationException($"'{solutionPath}' has no containing directory.");
        var existing = listed
            .Select(listedPath => Path.GetFullPath(Path.Combine(solutionDirectory, listedPath)))
            .Where(File.Exists);
        var root = AuthorizedRoot.Compute(solutionPath, existing);
        return Path.GetRelativePath(root, projectPath).Replace('\\', '/');
    }

    private static string? PathComponent(string projectId)
    {
        const string marker = ";path=";
        var start = projectId.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = projectId.IndexOf(';', start);
        return end < 0 ? projectId[start..] : projectId[start..end];
    }
}
