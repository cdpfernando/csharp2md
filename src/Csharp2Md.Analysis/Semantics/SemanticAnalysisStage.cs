using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Storage;

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

        const string configuration = "Debug";
        try
        {
            foreach (var targetFramework in context.DeclaredTargetFrameworks)
            {
                await using var lease = await _factory
                    .Open(context.SolutionPath, configuration, targetFramework, cancellationToken)
                    .ConfigureAwait(false);
                _ = lease.Diagnostics;
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

        return new StageResult(0, 0, 0, StructuralCorruption: false, HasUnknownsOrCandidatesOrFrontiers: false);
    }
}
