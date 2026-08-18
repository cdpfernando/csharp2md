using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;

namespace Csharp2Md.Core.Analysis.Semantics.MSBuild;

internal sealed record TrustedProjectEvaluationRequest(
    ProjectFactId ProjectId,
    string ProjectPath,
    TimeSpan ServiceTimeout)
{
    public static TrustedProjectEvaluationRequest Create(
        ProjectFactId projectId,
        string projectPath,
        AnalysisOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Validate() is { } error)
        {
            throw new ArgumentException(AnalysisOptions.MessageFor(error), nameof(options));
        }

        if (options is not { Mode: AnalysisMode.Semantic, Trust: TrustMode.TrustedSolution })
        {
            throw new ArgumentException("Project evaluation requires trusted semantic analysis.", nameof(options));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var absolutePath = Path.GetFullPath(projectPath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The project to evaluate does not exist.", absolutePath);
        }

        return new TrustedProjectEvaluationRequest(projectId, absolutePath, options.ServiceTimeout);
    }
}

internal sealed record EvaluatedItem(
    string Identity,
    ImmutableDictionary<string, string> Metadata);

internal sealed record EvaluatedTarget(
    TargetFactId TargetId,
    string TargetFramework,
    bool Succeeded,
    ImmutableDictionary<string, string> Properties,
    ImmutableDictionary<string, ImmutableArray<EvaluatedItem>> Items,
    ImmutableArray<AnalysisDiagnostic> Diagnostics);

internal sealed record ProjectEvaluationResult(
    ProjectFactId ProjectId,
    string DeclaredSdk,
    ImmutableArray<string> TargetFrameworks,
    ImmutableArray<string> EvaluatedImports,
    ImmutableArray<string> AnalyzerPaths,
    ImmutableArray<string> GeneratorPaths,
    ImmutableArray<EvaluatedTarget> Targets,
    ImmutableArray<AnalysisDiagnostic> Diagnostics,
    bool TimedOut);

internal interface IProjectEvaluationAdapter
{
    Task<ProjectEvaluationResult> EvaluateAsync(
        TrustedProjectEvaluationRequest request,
        CancellationToken cancellationToken);
}

