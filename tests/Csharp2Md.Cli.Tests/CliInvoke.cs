using System.CommandLine;
using Csharp2Md.Cli;
using CoreAnalyzeRequest = Csharp2Md.Core.AnalyzeRequest;
using CoreAnalyzeResult = Csharp2Md.Core.AnalyzeResult;
using CoreValidateRequest = Csharp2Md.Core.ValidateRequest;
using CoreValidateResult = Csharp2Md.Core.PackageValidationResult;

namespace Csharp2Md.Cli.Tests;

internal static class CliInvoke
{
    internal static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string[] args) =>
        await RunAsync(args, analyzeAsync: null, validatePackage: null);

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
}
