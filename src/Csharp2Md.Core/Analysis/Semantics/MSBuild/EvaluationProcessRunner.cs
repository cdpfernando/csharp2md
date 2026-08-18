using System.Diagnostics;

namespace Csharp2Md.Core.Analysis.Semantics.MSBuild;

internal sealed record EvaluationProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    ImmutableArray<string> Arguments);

internal interface IEvaluationProcessRunner
{
    Task<EvaluationProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken);
}

internal sealed class EvaluationProcessRunner : IEvaluationProcessRunner
{
    public async Task<EvaluationProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            throw;
        }

        return new EvaluationProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false),
            startInfo.ArgumentList.ToImmutableArray());
    }
}

