using System.CommandLine;
using Csharp2Md.Analysis;
using Csharp2Md.Cli;
using CoreAnalyzeRequest = Csharp2Md.Core.AnalyzeRequest;
using CoreAnalyzeResult = Csharp2Md.Core.AnalyzeResult;
using CoreDiagnostic = Csharp2Md.Core.EngineDiagnostic;
using CoreValidateRequest = Csharp2Md.Core.ValidateRequest;
using CoreValidateResult = Csharp2Md.Core.PackageValidationResult;

namespace Csharp2Md.Cli.Tests;

internal static class CliInvoke
{
    internal static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string[] args,
        IAnalysisEngine? engine = null)
    {
        Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>>? analyzeAsync =
            engine is null ? null : Adapt(engine);
        return await RunAsync(args, analyzeAsync, validatePackage: null);
    }

    internal static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string[] args,
        Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>>? analyzeAsync) =>
        await RunAsync(args, analyzeAsync, validatePackage: null);

    internal static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string[] args,
        Func<CoreValidateRequest, CoreValidateResult> validatePackage) =>
        await RunAsync(args, analyzeAsync: null, validatePackage);

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string[] args,
        Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>>? analyzeAsync,
        Func<CoreValidateRequest, CoreValidateResult>? validatePackage)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var configuration = new InvocationConfiguration
        {
            Output = stdout,
            Error = stderr,
        };

        var exitCode = await CommandFactory.InvokeAsync(args, analyzeAsync, validatePackage, configuration);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private static Func<CoreAnalyzeRequest, CancellationToken, Task<CoreAnalyzeResult>> Adapt(
        IAnalysisEngine engine) =>
        async (request, cancellationToken) =>
        {
            var legacyRequest = AnalysisRequest.Create(request.SolutionPaths);
            var result = await engine.AnalyzeAsync(legacyRequest, cancellationToken);
            var diagnostics = result.Solutions
                .Where(static outcome => outcome.Status == PublicationStatus.Unpublished)
                .Select(static outcome => new CoreDiagnostic(
                    "legacy-analysis-rejected",
                    outcome.FailingStage ?? "analysis",
                    outcome.Detail ?? "unpublished-solution",
                    solution: outcome.LogicalRelativePath))
                .ToImmutableArray();

            if (result.HasBatchPublicationFailure)
            {
                diagnostics = diagnostics.Add(new CoreDiagnostic(
                    "legacy-analysis-rejected",
                    result.BatchPublicationGate ?? "publication",
                    result.BatchPublicationDetail ?? "batch-publication-failed"));
            }

            return new CoreAnalyzeResult(!result.HasUnpublishedSolution && !result.HasBatchPublicationFailure, diagnostics);
        };
}
